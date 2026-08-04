using System.Numerics;
using BOCCHI.Common.Targeting;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;

namespace BOCCHI.Automator.StateMachine.Handlers;

internal static class CombatActivityHandler
{
    /// <summary>Matches BossMod StayCloseToTarget OnHitbox (±1y).</summary>
    private const float HitboxEdgeTolerance = 1f;

    /// <summary>Ranged/healer/caster standoff past the target hitbox.</summary>
    private const float RangedStandoffRange = 15f;

    /// <summary>Dismount once this close (travel convenience).</summary>
    private const float DismountRange = 20f;

    /// <returns>True when the initial approach is done (in range, or combat took over).</returns>
    public static bool HandleTargets(
        IGameObject player,
        IPlayer playerState,
        IEnumerable<IBattleNpc> targets,
        ICondition conditions,
        IPathfinder pathfinder,
        string throttlePrefix,
        bool shouldApproachTarget,
        bool stopPathfinderInCombat = false,
        bool deferCombatToBossModAi = false
    )
    {
        List<IBattleNpc> list = targets as List<IBattleNpc> ?? targets.ToList();
        // Illegal Mode / Pots: nearest enemy only. Pack-center targeting is Mob Farmer.
        IBattleNpc? target = TargetHelper.Select(list, preferCentroid: false);
        if (target == null)
        {
            return false;
        }

        bool isMelee = playerState.IsMelee();
        float distance = player.Position.Distance2D(target.Position) - target.HitboxRadius;

        if (conditions[ConditionFlag.Mounted]
            && distance <= DismountRange
            && EzThrottler.Throttle($"{throttlePrefix}::Unmount")
            && Actions.Unmount.CanCast())
        {
            Actions.Unmount.Cast();
            pathfinder.Stop();
            return false;
        }

        if (deferCombatToBossModAi)
        {
            // Hand off once we're in role range so StayCloseToTarget isn't starting from afar.
            if (IsInEngagementRange(distance, isMelee) || conditions[ConditionFlag.InCombat])
            {
                pathfinder.Stop();
                return true;
            }

            if (!shouldApproachTarget
                || !EzThrottler.Throttle($"{throttlePrefix}::Approach", 500)
                || !pathfinder.IsIdle())
            {
                return false;
            }

            PathToEngagement(player, target, isMelee, pathfinder);
            return false;
        }

        if (stopPathfinderInCombat && conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
            return true;
        }

        if (IsInEngagementRange(distance, isMelee))
        {
            pathfinder.Stop();
            return true;
        }

        if (!shouldApproachTarget
            || !EzThrottler.Throttle($"{throttlePrefix}::Approach", 500)
            || !pathfinder.IsIdle())
        {
            return false;
        }

        PathToEngagement(player, target, isMelee, pathfinder);
        return false;
    }

    private static bool IsInEngagementRange(float distancePastHitbox, bool isMelee)
    {
        // Melee: at hitbox edge. Ranged: reached (or inside) the 15y ring — BossMod maintains it.
        return isMelee
            ? distancePastHitbox <= HitboxEdgeTolerance
            : distancePastHitbox <= RangedStandoffRange + 0.5f;
    }

    private static void PathToEngagement(
        IGameObject player,
        IBattleNpc target,
        bool isMelee,
        IPathfinder pathfinder
    )
    {
        if (isMelee)
        {
            pathfinder.PathfindAndMoveTo(new PathfinderConfig(target.Position)
            {
                DistanceThreshold = Math.Max(0.5f, target.HitboxRadius + HitboxEdgeTolerance),
                ShouldSnapToFloor = true,
            });
            return;
        }

        // Path to a point on the 15y ring (past hitbox), not into the center.
        Vector3 standOff = target.Position.GetApproachPosition(
            player.Position,
            target.HitboxRadius + RangedStandoffRange);
        pathfinder.PathfindAndMoveTo(new PathfinderConfig(standOff)
        {
            DistanceThreshold = 1f,
            ShouldSnapToFloor = true,
        });
    }
}
