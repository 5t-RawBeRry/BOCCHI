using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

/// <summary>
///     Shared mount helpers: cast while pathing (mount is usable on the move).
/// </summary>
public static class MountWait
{
    /// <summary>Legacy cap for callers that still poll mount readiness.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    /// <summary>Legacy grace before walking when mount never starts.</summary>
    public static readonly TimeSpan StartGrace = TimeSpan.FromSeconds(1.5);

    private static DateTime lastTryCastUtc = DateTime.MinValue;

    private static readonly TimeSpan TryCastInterval = TimeSpan.FromMilliseconds(750);

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

    /// <summary>Cast mount while pathing if far enough and not already mounted.</summary>
    public static void TryCastIfNeeded(
        ICondition conditions,
        IObjectTable objects,
        Vector3 destination,
        bool autoMountEnabled = true,
        uint preferredMountId = 0)
    {
        if (ShouldSkip(conditions, objects, destination, autoMountEnabled))
        {
            return;
        }

        if (DateTime.UtcNow - lastTryCastUtc < TryCastInterval)
        {
            return;
        }

        lastTryCastUtc = DateTime.UtcNow;
        TryCast(preferredMountId);
    }

    /// <summary>
    ///     Returns true when ready to pathfind (mounted, mounting, or giving up to walk).
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
        // Ready once mounted or mount animation started — pathfind during Mounting (#130).
        if (!autoMountEnabled
            || conditions[ConditionFlag.Mounted]
            || conditions[ConditionFlag.Mounting])
        {
            return true;
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
