using BOCCHI.Common.Config;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Ocelot.Services.PlayerState;
using Action = Ocelot.Actions.Action;

namespace BOCCHI.MobFarmer.Services;

/// <summary>Tank ranged / Provoke / gap closer during Gathering. No-op on non-tanks.</summary>
public sealed class FarmerPullAssist(MobFarmerConfig config, IPlayer player, ITargetManager targets)
{
    public const float PullRange = 20f;

    private static readonly Action Provoke = new(ActionType.Action, 7533);

    public bool TryPull(IBattleNpc current)
    {
        if (!player.IsTank())
        {
            return false;
        }

        if (!EzThrottler.Throttle("MobFarmer::Pull", 400))
        {
            return false;
        }

        if (config.ShouldHandleTargeting)
        {
            targets.Target = current;
        }

        bool used = false;
        if (config.UseRangedPull && TryRanged() is { } ranged && ranged.CanCast())
        {
            used = ranged.Cast();
        }

        if (config.UseProvoke && Provoke.CanCast())
        {
            used = Provoke.Cast() || used;
        }

        if (config.UseGapCloser && TryGapCloser() is { } gap && gap.CanCast())
        {
            used = gap.Cast() || used;
        }

        return used;
    }

    private Action? TryRanged()
    {
        uint? jobId = player.GetClassJob()?.RowId;
        return jobId switch
        {
            19 or 1 => new(ActionType.Action, 24), // PLD / GLA Shield Lob
            21 or 3 => new(ActionType.Action, 31), // WAR / MRD Tomahawk
            32 => new(ActionType.Action, 3624), // DRK Unmend
            37 => new(ActionType.Action, 16139), // GNB Lightning Shot
            _ => null,
        };
    }

    private Action? TryGapCloser()
    {
        uint? jobId = player.GetClassJob()?.RowId;
        return jobId switch
        {
            19 => new(ActionType.Action, 16461), // Intervene
            21 => new(ActionType.Action, 7387), // Onslaught
            32 => new(ActionType.Action, 36926), // Shadowstride
            37 => new(ActionType.Action, 36934), // Trajectory
            _ => null,
        };
    }
}
