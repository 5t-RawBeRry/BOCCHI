using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using Ocelot.Actions;

namespace BOCCHI.Common.Data.Zones;

/// <summary>Shared dismount helper for state handlers.</summary>
public static class DismountAssist
{
    /// <summary>
    ///     If mounted, mounting, or still in the dismount jump/landing, try to dismount / wait.
    ///     Returns true when the caller should wait (not act yet).
    /// </summary>
    public static bool TryDismount(ICondition conditions)
    {
        // Dismount leaves a jump/landing beat — actions then fail with "while jumping".
        // Prefer ECommons IsJumping (condition flags + Character->IsJumping) over full Player.IsBusy,
        // which also treats moving / combat / casting as busy and would stall pathing callers.
        if (Player.IsJumping)
        {
            return true;
        }

        if (!conditions[ConditionFlag.Mounted] && !conditions[ConditionFlag.Mounting])
        {
            return false;
        }

        // Throttle and check CanCast, the same as every other unmount site. Casting once per tick
        // had the game reject the action outright, and since TryDismount keeps reporting "still
        // preparing" the caller waits for a dismount that never lands — pot reveals never opened.
        if (!conditions[ConditionFlag.Mounting]
            && EzThrottler.Throttle("DismountAssist::Dismount", 500)
            && Actions.Dismount.CanCast())
        {
            Actions.Dismount.Cast();
        }

        return true;
    }
}
