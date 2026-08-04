using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
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
    IAutomatorMemory memory
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
        if (objects.LocalPlayer is null)
        {
            return;
        }

        // Illegal Mode open combat: BOCCHI AI owns targeting when enabled; otherwise leave target alone.
        // (ShouldHandleTargeting is Mob Farmer only.)

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
