using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
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
    ISupportJobFactory supportJobs
)
{
    public void PrepareForIllegalMode()
    {
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

    /// <summary>Drop combat automation while travelling. No-op if already in a FATE/CE.</summary>
    public void DisableAi()
    {
        if (criticalEncounters.IsInCriticalEncounter() || fates.IsInFate())
        {
            return;
        }

        session.Disable();
    }

    public void Tick()
    {
        SyncActivityCombat();
        session.Tick(CurrentPhantomJobId());
    }

    private void SyncActivityCombat()
    {
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
                if (!pluginStatus.IsLoaded(JobRotationBackendKeys.RotationSolverReborn))
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
