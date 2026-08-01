using BOCCHI.Common.Config;
using BOCCHI.Common.Targeting;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Automator.StateMachine.Handlers;

internal static class CombatActivityHandler
{
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
        if (distance <= 5f && conditions[ConditionFlag.Mounted])
        {
            if (EzThrottler.Throttle($"{throttlePrefix}::Unmount") && Actions.Unmount.CanCast())
            {
                Actions.Unmount.Cast();
                pathfinder.Stop();
            }
        }

        if (stopPathfinderInCombat && conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
        }
    }
}
