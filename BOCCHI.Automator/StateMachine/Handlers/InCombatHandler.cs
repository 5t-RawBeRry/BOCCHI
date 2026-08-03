using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Common.Targeting;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InCombatHandler
(
    IObjectTable objects,
    ICondition conditions,
    IFateContext fateContext,
    ICriticalEncounterContext criticalEncounterContext,
    IPathfinder pathfinder,
    IAutomatorMemory memory,
    CombatConfig combat,
    AutomatorConfig automatorConfig,
    ITargetManager targetManager
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InCombat)
{
    public override StatePriority GetScore()
    {
        if (objects.LocalPlayer == null)
        {
            return StatePriority.Never;
        }

        if (criticalEncounterContext.IsInCriticalEncounter() || fateContext.IsInFate())
        {
            return StatePriority.Never;
        }

        // Don't abandon Fate/CE transit to fight random trash on the road.
        if (memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _))
        {
            return StatePriority.Never;
        }

        return conditions[ConditionFlag.InCombat] ? StatePriority.High : StatePriority.Never;
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        // When BOCCHI AI is used, VBM AutoTarget owns targeting.
        if (!automatorConfig.ToggleAiProvider
            && combat.ShouldHandleTargeting
            && EzThrottler.Throttle("InCombat::Target", 250))
        {
            IBattleNpc? target = TargetHelper.Select(
                TargetHelper.GetHostileEnemies(objects, player.Position),
                combat.ForceTargetCentralEnemy);

            if (target != null && targetManager.Target?.GameObjectId != target.GameObjectId)
            {
                targetManager.Target = target;
            }
        }

        if (conditions[ConditionFlag.Mounted])
        {
            if (EzThrottler.Throttle("InCombat::Unmount") && Actions.Unmount.CanCast())
            {
                Actions.Unmount.Cast();
                pathfinder.Stop();
            }
        }
    }
}
