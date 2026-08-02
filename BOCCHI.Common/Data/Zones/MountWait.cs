using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

/// <summary>
///     Shared mount-before-pathfind wait: try preferred mount or Mount Roulette briefly, then walk.
///     Avoids burning a long timeout when mount never starts (UI/busy/cast lock).
/// </summary>
public static class MountWait
{
    /// <summary>Max wait while Mounting, or overall cap.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    /// <summary>If mount cast never enters Mounting within this, pathfind on foot.</summary>
    public static readonly TimeSpan StartGrace = TimeSpan.FromSeconds(1.5);

    public static bool ShouldSkip(
        ICondition conditions,
        IObjectTable objects,
        Vector3 destination,
        bool autoMountEnabled = true)
    {
        if (!autoMountEnabled)
        {
            return true;
        }

        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            return true;
        }

        if (conditions[ConditionFlag.InCombat] || conditions[ConditionFlag.Unconscious])
        {
            return true;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return true;
        }

        return player.Position.Distance(destination) <= NavigationConstants.MountMinDistance;
    }

    /// <param name="preferredMountId">Mount sheet row ID; 0 = Mount Roulette.</param>
    public static void TryCast(uint preferredMountId = 0)
    {
        if (preferredMountId != 0)
        {
            Ocelot.Actions.Action mount = Actions.Mount(preferredMountId);
            if (mount.CanCast())
            {
                mount.Cast();
            }

            return;
        }

        if (Actions.MountRoulette.CanCast())
        {
            Actions.MountRoulette.Cast();
        }
    }

    /// <summary>
    ///     Returns true when ready to pathfind (mounted or giving up to walk).
    ///     <paramref name="started"/> is when the wait began (UtcNow).
    /// </summary>
    public static bool IsReadyOrGiveUp(
        ICondition conditions,
        IObjectTable objects,
        Vector3 destination,
        DateTime started,
        bool autoMountEnabled = true,
        uint preferredMountId = 0)
    {
        if (!autoMountEnabled || conditions[ConditionFlag.Mounted])
        {
            return true;
        }

        // Mount cast in progress — wait for Mounted (capped by Timeout on the WaitUntil).
        if (conditions[ConditionFlag.Mounting])
        {
            return false;
        }

        if (conditions[ConditionFlag.InCombat] || conditions[ConditionFlag.Unconscious])
        {
            return true;
        }

        if (objects.LocalPlayer is not { } player
            || player.Position.Distance(destination) <= NavigationConstants.MountMinDistance)
        {
            return true;
        }

        TryCast(preferredMountId);

        // Never entered Mounting — walk instead of sitting on the full timeout.
        return DateTime.UtcNow - started >= StartGrace;
    }
}
