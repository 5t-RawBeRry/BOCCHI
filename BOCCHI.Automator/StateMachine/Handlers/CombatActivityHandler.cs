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

    public static void HandleTargets(
        IGameObject player,
        IEnumerable<IBattleNpc> targets,
        CombatConfig combat,
        ITargetManager targetManager,
        ICondition conditions,
        IPathfinder pathfinder,
        string throttlePrefix,
        bool stopPathfinderInCombat = false
    )
    {
        List<IBattleNpc> list = targets as List<IBattleNpc> ?? targets.ToList();
        IBattleNpc? target = TargetHelper.Select(list, combat.ForceTargetCentralEnemy);
        if (target == null)
        {
            return;
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

        // Walk into max melee of the boss/pack — don't sit at the FATE circle edge (~20y).
        if (distance > MaxMeleeRange)
        {
            if (EzThrottler.Throttle($"{throttlePrefix}::Approach", 500) && pathfinder.IsIdle())
            {
                float arrival = Math.Max(0.5f, target.HitboxRadius + MaxMeleeRange);
                pathfinder.PathfindAndMoveTo(new PathfinderConfig(target.Position)
                {
                    DistanceThreshold = arrival,
                    ShouldSnapToFloor = true,
                });
            }

            return;
        }

        pathfinder.Stop();

        if (stopPathfinderInCombat && conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
        }
    }
}
