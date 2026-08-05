using BOCCHI.Common;
using BOCCHI.Common.Config;
using Dalamud.Plugin.Services;
using Ocelot.Rotation.Services.BossMod;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Automator.Services;

/// <summary>Owns the ephemeral BOCCHI AI BossMod/VBM preset while Illegal Mode is on.</summary>
public class AutoRotationController(
    BossModRotationService bossMod,
    AutomatorConfig config,
    UIConfig uiConfig,
    IPlayer player,
    IChatGui chat
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
    }

    public void TeardownForIllegalMode()
    {
        if (!config.ToggleAiProvider)
        {
            return;
        }

        bossMod.DestroyAutoRotationPreset();
    }

    public void EnableForActivity()
    {
        if (!config.ToggleAiProvider)
        {
            return;
        }

        bossMod.EnableAutoRotation();
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

        bossMod.Refresh();
    }

    private void PrintNotReady()
    {
        var job = player.GetClassJob();
        chat.PrintError(BocchiChat.Format(
            $"BOCCHI AI not ready (is BossMod / BMR loaded?). "
            + $"job={job?.Abbreviation.ToString() ?? "?"} melee={player.IsMelee()}",
            uiConfig));
    }
}
