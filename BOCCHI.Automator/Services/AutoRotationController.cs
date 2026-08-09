using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Rotation.Services.BossMod;
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
    IFateContext fates
)
{
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

    public void DisableForTravel()
    {
        if (!config.ToggleAiProvider)
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
        if (criticalEncounters.IsInCriticalEncounter())
        {
            bossMod.EnableForActivity(BocchiAiActivity.CriticalEncounter);
            return;
        }

        if (fates.IsInFate())
        {
            bossMod.EnableForActivity(BocchiAiActivity.Fate);
        }
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
