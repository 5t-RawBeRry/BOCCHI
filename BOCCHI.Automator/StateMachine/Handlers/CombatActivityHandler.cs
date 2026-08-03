using BOCCHI.Common.Config;
using BOCCHI.Common.Targeting;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Automator.StateMachine.Handlers;

internal static class CombatActivityHandler
{
    /// <summary>Standard max-melee distance past the target hitbox.</summary>
    private const float MaxMeleeRange = 3f;

    /// <returns>
    ///     <see langword="true"/> once the activity's initial approach was issued,
    ///     was already unnecessary, or was superseded by entering combat.
    /// </returns>
    public static bool HandleTargets(
        IGameObject player,
        IEnumerable<IBattleNpc> targets,
        CombatConfig combat,
        ITargetManager targetManager,
        ICondition conditions,
        IPathfinder pathfinder,
        string throttlePrefix,
        bool shouldApproachTarget,
        bool stopPathfinderInCombat = false
    )
    {
        List<IBattleNpc> list = targets as List<IBattleNpc> ?? targets.ToList();
        IBattleNpc? target = TargetHelper.Select(list, combat.ForceTargetCentralEnemy);
        if (target == null)
        {
            return false;
        }

        if (combat.ShouldHandleTargeting
            && EzThrottler.Throttle($"{throttlePrefix}::Target")
            && targetManager.Target?.GameObjectId != target.GameObjectId)
        {
            targetManager.Target = target;
        }

        float distance = player.Position.Distance2D(target.Position) - target.HitboxRadius;
        if (distance <= MaxMeleeRange && conditions[ConditionFlag.Mounted])
        {
            if (EzThrottler.Throttle($"{throttlePrefix}::Unmount") && Actions.Unmount.CanCast())
            {
                Actions.Unmount.Cast();
                pathfinder.Stop();
            }
        }

        // Once combat has started, FATE movement is deliberately handed back to the player.
        // This must precede the out-of-range branch so that branch cannot re-start navigation.
        if (stopPathfinderInCombat && conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
            return true;
        }

        if (distance <= MaxMeleeRange)
        {
            pathfinder.Stop();
            return true;
        }

        // Only issue the max-melee movement once for this FATE/CE. The caller keeps
        // this completed across handler re-entry and re-arms it for a different event.
        if (!shouldApproachTarget
            || !EzThrottler.Throttle($"{throttlePrefix}::Approach", 500)
            || !pathfinder.IsIdle())
        {
            return false;
        }

        float arrival = Math.Max(0.5f, target.HitboxRadius + MaxMeleeRange);
        pathfinder.PathfindAndMoveTo(new PathfinderConfig(target.Position)
        {
            DistanceThreshold = arrival,
            ShouldSnapToFloor = true,
        });

        return true;
    }
}
