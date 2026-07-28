using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class WaitingForCriticalEncounterHandler
(
    IAutomatorMemory memory,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    IChainManager manager,
    ICriticalEncounterRepository repo
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.WaitingForCriticalEncounter)
{
    public override StatePriority GetScore()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return StatePriority.Never;
        }

        // See if we have a goal in memory and that goal is a CE
        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal) || goal.Goal.GoalType is not CriticalEncounterGoal ceGoal)
        {
            return StatePriority.Never;
        }

        // See if that ce goal memory is an active CE that is currently preparing to launch
        CriticalEncounter? ce = repo.SnapshotWithoutForkedTower().FirstOrDefault(ce => ce.Id == ceGoal.id);
        if (ce == null || !ce.IsPreparing())
        {
            return StatePriority.Never;
        }

        float radius = ce.Radius;
        float distance = player.Position.Distance2D(ce.Position);
        float percent = distance / radius;

        if (percent >= 1.5f)
        {
            return StatePriority.Never;
        }

        if (percent >= 0.95f)
        {
            return StatePriority.Normal;
        }

        if (percent >= 0.85f)
        {
            return StatePriority.AboveNormal;
        }

        return StatePriority.VeryHigh;
    }

    public override void Enter()
    {
        base.Enter();
        manager.CancelAll();
        memory.Forget<GoalPathStepMemory>();
        memory.TryAdd<WaitingForCriticalEncounterMemory>();
        pathfinder.Stop();
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal) || goal.Goal.GoalType is not CriticalEncounterGoal ceGoal)
        {
            return;
        }

        CriticalEncounter? ce = repo.SnapshotWithoutForkedTower().FirstOrDefault(ce => ce.Id == ceGoal.id);
        if (ce == null || !ce.IsPreparing())
        {
            return;
        }

        float radius = ce.Radius;
        float distance = player.Position.Distance2D(ce.Position);
        float percent = distance / radius;

        if (percent >= 1.0f)
        {
            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new(ce.Position.GetApproachPosition(player.Position, ce.Radius * 0.8f, 30f)));
            }

            return;
        }

        if (conditions[ConditionFlag.Mounted])
        {
            if (EzThrottler.Throttle("WaitingForCriticalEncounter::Unmount") && Actions.Unmount.CanCast())
            {
                Actions.Unmount.Cast();
                pathfinder.Stop();
            }
        }
    }
}
