using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InFateHandler
(
    IAutomatorMemory memory,
    IFateContext context,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    CombatConfig combat,
    ITargetManager targetManager
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InFate)
{
    public override StatePriority GetScore()
    {
        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal) || goal.Goal.GoalType is not FateGoal fateGoal)
        {
            return StatePriority.Never;
        }

        return context.GetFateId() == fateGoal.id ? StatePriority.High : StatePriority.Never;
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        CombatActivityHandler.HandleTargets(
            player,
            context.GetTargets().ToList(),
            combat,
            targetManager,
            conditions,
            pathfinder,
            "InFate",
            true
        );
    }
}
