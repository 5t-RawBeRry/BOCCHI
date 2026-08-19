using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Ipc.RotationSolverReborn;
using Ocelot.Rotation.Services;
using Ocelot.Services.PlayerState;
using Ocelot.Services.PluginStatus;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Illegal Mode adapter: config → <see cref="ICombatRotationSession"/> plus FATE/CE/travel hooks.
/// </summary>
public class AutoRotationController(
    ICombatRotationSession session,
    AutomatorConfig config,
    UIConfig uiConfig,
    IPlayer player,
    IChatGui chat,
    ICriticalEncounterContext criticalEncounters,
    IFateContext fates,
    IPluginStatus pluginStatus,
    IRotationSolverRebornIpc rsr,
    ISupportJobFactory supportJobs,
    IAutomatorMemory memory
)
{
    /// <summary>
    ///     Pot chest farming owns the character outright — it walks to reveals and opens them, and
    ///     leftover AutoTarget / AI movement from the pot FATE fights that. The FATE is usually still
    ///     up while farming, so this has to beat the in-activity guards below.
    /// </summary>
    private bool CombatSuppressedByActivity =>
        memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
        || memory.TryRemember<PendingPotChestFarmMemory>(out PendingPotChestFarmMemory _);

    public void PrepareForIllegalMode()
    {
        session.OverwriteBossModPresets = config.UpdateBossModPresetsAutomatically;
        session.MovementSettings = BossModMovement.From(config, player.IsMelee());
        if (!config.CombatAutorotation.UsesCombatAutomation() || !ValidatePluginsForConfig())
        {
            return;
        }

        session.Prepare(ToRecipe(config.CombatAutorotation));
        session.Tick(CurrentPhantomJobId());
        SyncActivityCombat();
    }

    public void TeardownForIllegalMode() => session.Teardown();

    public void EnableForFate() => session.Enable(CombatActivity.Fate);

    public void EnableForCriticalEncounter() => session.Enable(CombatActivity.CriticalEncounter);

    /// <summary>
    ///     Fight back while pot chest farming. The AI deliberately owns movement here: the farm does
    ///     not path during combat, so there is nothing to fight over — and the magic pot trails the
    ///     player, so letting the AI dodge takes the pot out of AoE with us. The pot can be
    ///     destroyed and the run lost with it, which is the real reason this matters (#188).
    /// </summary>
    public void EnableForSelfDefence() => session.Enable(CombatActivity.Fate);

    /// <summary>
    ///     Drop combat automation while travelling. Normally a no-op inside a FATE/CE, since the
    ///     activity still wants the rotation — except when pot chest farming has taken over.
    /// </summary>
    public void DisableAi()
    {
        if (!CombatSuppressedByActivity
            && (criticalEncounters.IsInCriticalEncounter() || fates.IsInFate()))
        {
            return;
        }

        session.Disable();
    }

    public void Tick()
    {
        session.OverwriteBossModPresets = config.UpdateBossModPresetsAutomatically;
        session.MovementSettings = BossModMovement.From(config, player.IsMelee());
        SyncActivityCombat();
        session.Tick(CurrentPhantomJobId());
    }

    private void SyncActivityCombat()
    {
        // Without this the per-tick sync re-enables the rotation immediately: pot chest farming
        // usually runs while still standing in the pot FATE.
        if (CombatSuppressedByActivity)
        {
            return;
        }

        // Pathfinding / CE wait own the character. Tick would re-Enable AutoTarget and pull
        // trash on the road (or at the registration rim) while we are still walking in.
        if (memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _)
            || memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory _)
            || memory.TryRemember<WaitingForPotFateMemory>(out WaitingForPotFateMemory _))
        {
            return;
        }

        if (criticalEncounters.IsInCriticalEncounter())
        {
            session.Enable(CombatActivity.CriticalEncounter);
            return;
        }

        if (fates.IsInFate())
        {
            session.Enable(CombatActivity.Fate);
        }
    }

    private static CombatRotationRecipe ToRecipe(CombatAutorotation value) => value switch
    {
        CombatAutorotation.WrathCombo => new(
            JobRotationBackendKind.Wrath,
            CombatAiKind.MiscAi,
            ManualTargeting: true),
        CombatAutorotation.RotationSolverReborn => new(
            JobRotationBackendKind.RotationSolverReborn,
            CombatAiKind.MiscAi,
            ManualTargeting: true),
        CombatAutorotation.BossMod => new(JobRotationBackendKind.BossMod, CombatAiKind.None),
        CombatAutorotation.BossModReborn => new(JobRotationBackendKind.BossModReborn, CombatAiKind.None),
        _ => CombatRotationRecipe.None,
    };

    private bool ValidatePluginsForConfig()
    {
        switch (config.CombatAutorotation)
        {
            case CombatAutorotation.WrathCombo:
                if (!pluginStatus.IsLoaded(JobRotationBackendKeys.Wrath))
                {
                    PrintJobProviderMissing("Wrath Combo");
                    return false;
                }

                WarnIfBossModMissing();
                return true;

            case CombatAutorotation.RotationSolverReborn:
                if (!CombatPluginPresence.RotationSolverReborn(pluginStatus, rsr))
                {
                    PrintJobProviderMissing("Rotation Solver Reborn");
                    return false;
                }

                WarnIfBossModMissing();
                return true;

            case CombatAutorotation.BossMod:
                return ValidateBossModFork(
                    required: JobRotationBackendKeys.BossMod,
                    other: JobRotationBackendKeys.BossModReborn,
                    requiredLabel: "BossMod",
                    otherLabel: "BossMod Reborn");

            case CombatAutorotation.BossModReborn:
                return ValidateBossModFork(
                    required: JobRotationBackendKeys.BossModReborn,
                    other: JobRotationBackendKeys.BossMod,
                    requiredLabel: "BossMod Reborn",
                    otherLabel: "BossMod");

            default:
                return false;
        }
    }

    private bool ValidateBossModFork(string required, string other, string requiredLabel, string otherLabel)
    {
        if (pluginStatus.IsLoaded(required))
        {
            return true;
        }

        if (pluginStatus.IsLoaded(other))
        {
            BocchiChat.PrintError(
                chat,
                uiConfig,
                $"Combat autorotation is set to {requiredLabel}, but only {otherLabel} is loaded.");
        }
        else
        {
            PrintJobProviderMissing(requiredLabel);
        }

        return false;
    }

    private void WarnIfBossModMissing()
    {
        if (!pluginStatus.IsLoaded(JobRotationBackendKeys.BossMod)
            && !pluginStatus.IsLoaded(JobRotationBackendKeys.BossModReborn))
        {
            var job = player.GetClassJob();
            BocchiChat.PrintError(
                chat,
                uiConfig,
                $"BOCCHI AI / BossMod autorotation not ready (is BossMod / BMR loaded?). "
                + $"job={job?.Abbreviation.ToString() ?? "?"} melee={player.IsMelee()}");
        }
    }

    private uint? CurrentPhantomJobId() =>
        supportJobs.TryGetCurrent(out SupportJob current) ? current.Id.RowId() : null;

    private void PrintJobProviderMissing(string name)
    {
        BocchiChat.PrintError(
            chat,
            uiConfig,
            $"Combat autorotation needs {name}, but that plugin is not loaded.");
    }
}
