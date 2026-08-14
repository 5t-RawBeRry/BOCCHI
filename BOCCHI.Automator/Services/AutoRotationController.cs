using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Rotation.Services.BossMod;
using Ocelot.Services.Logger;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Automator.Services;

/// <summary>Owns ephemeral BOCCHI AI BossMod/VBM presets while Illegal Mode is on.</summary>
public class AutoRotationController(
    BossModRotationService bossMod,
    AutomatorConfig config,
    UIConfig uiConfig,
    IPlayer player,
    IChatGui chat,
    ICriticalEncounterContext criticalEncounters,
    IFateContext fates,
    ILogger<AutoRotationController> logger
)
{
    private string? lastAiDecision;

    public void PrepareForIllegalMode()
    {
        if (!config.ToggleAiProvider)
        {
            return;
        }

        if (!bossMod.TryEnsureBocchiAiPreset(out _))
        {
            PrintNotReady();
        }

        // Hot reload mid-CE/FATE: Enter may have already run (or EventId lags). Arm AI now.
        SyncActivityAi();
    }

    public void TeardownForIllegalMode()
    {
        if (!config.ToggleAiProvider)
        {
            return;
        }

        bossMod.DestroyAutoRotationPreset();
    }

    public void EnableForFate()
    {
        if (!config.ToggleAiProvider)
        {
            return;
        }

        bossMod.EnableForActivity(BocchiAiActivity.Fate);
    }

    public void EnableForCriticalEncounter()
    {
        if (!config.ToggleAiProvider)
        {
            return;
        }

        bossMod.EnableForActivity(BocchiAiActivity.CriticalEncounter);
    }

    /// <summary>
    ///     Hand control back to plain BossMod while travelling. Do not call this inside a FATE/CE —
    ///     the preset is what provides targeting, range and dodging, so deactivating it there stops
    ///     the character dodging at all.
    /// </summary>
    public void DisableAi()
    {
        if (!config.ToggleAiProvider)
        {
            return;
        }

        // Never strip the preset while we are actually in a FATE/CE. Travel states call this from
        // Enter(), and they can briefly win the score — most obviously when Illegal Mode is switched
        // on mid-fight, because InFate/InCriticalEncounter need a GoalMemory that does not exist yet,
        // so Pathfinding/Idle enters first and deactivated the preset we had just armed.
        if (criticalEncounters.IsInCriticalEncounter() || fates.IsInFate())
        {
            return;
        }

        bossMod.DisableAutoRotation();
    }

    public void Tick()
    {
        if (!config.ToggleAiProvider)
        {
            return;
        }

        // Pathfinding Enter disables AI; if EventId already says we're in the CE/FATE,
        // re-arm so a plugin reload mid-fight does not leave the preset inactive.
        SyncActivityAi();
        bossMod.Refresh();
    }

    private void SyncActivityAi()
    {
        string decision;

        if (criticalEncounters.IsInCriticalEncounter())
        {
            bossMod.EnableForActivity(BocchiAiActivity.CriticalEncounter);
            decision = "in CE — arming BOCCHI AI CE preset";
        }
        else if (fates.IsInFate())
        {
            bossMod.EnableForActivity(BocchiAiActivity.Fate);
            decision = "in FATE — arming BOCCHI AI FATE preset";
        }
        else
        {
            decision = "not in a FATE or CE — AI preset left alone";
        }

        // Edge-triggered: this runs every tick, so only log when the answer changes.
        if (decision == lastAiDecision)
        {
            return;
        }

        lastAiDecision = decision;
        logger.Info("BOCCHI AI: {Decision}", decision);
    }

    private void PrintNotReady()
    {
        var job = player.GetClassJob();
        BocchiChat.PrintError(
            chat,
            uiConfig,
            $"BOCCHI AI not ready (is BossMod / BMR loaded?). "
            + $"job={job?.Abbreviation.ToString() ?? "?"} melee={player.IsMelee()}");
    }
}
