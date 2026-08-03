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

    /// <returns>True when the initial approach is done (in melee, or combat took over).</returns>
    public static bool HandleTargets(
        IGameObject player,
        IEnumerable<IBattleNpc> targets,
        CombatConfig combat,
        ITargetManager targetManager,
        ICondition conditions,
        IPathfinder pathfinder,
        string throttlePrefix,
        bool shouldApproachTarget,
        bool stopPathfinderInCombat = false,
        bool deferCombatToBossModAi = false
    )
    {
        // BOCCHI AI (VBM AutoTarget + NormalMovement) owns targeting and combat movement.
        if (deferCombatToBossModAi)
        {
            pathfinder.Stop();
            return true;
        }

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

        // Hand movement back once combat starts (before the out-of-range branch can repath).
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

        // One max-melee approach per FATE/CE — don't keep pulling the player back in.
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

        // Still pending until we arrive (or combat takes over) so a failed pathfind can retry.
        return false;
    }
}
