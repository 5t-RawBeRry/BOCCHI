using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;
namespace BOCCHI.Automator.StateMachine.Handlers;

public class InCriticalEncounterHandler
(
    IAutomatorMemory memory,
    ICriticalEncounterContext context,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    CombatConfig combat,
    ITargetManager targetManager
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InCriticalEncounter)
{
    public override StatePriority GetScore() => context.IsInCriticalEncounter() ? StatePriority.High : StatePriority.Never;

    public override void Enter()
    {
        base.Enter();
        memory.Forget<WaitingForCriticalEncounterMemory>();
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
            "InCriticalEncounter"
        );
    }
}
