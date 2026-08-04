using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
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
using System.Numerics;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class WaitingForCriticalEncounterHandler
(
    IAutomatorMemory memory,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    IChainManager manager,
    ICriticalEncounterRepository repo,
    AutomatorConfig config
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

        // ce.Radius includes padding; score against the real combat circle.
        float combatRadius = ce.Radius - NavigationConstants.CriticalEncounterRadiusPadding;
        if (combatRadius <= 0f)
        {
            return StatePriority.Never;
        }

        float percent = player.Position.Distance2D(ce.Position) / combatRadius;

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

        float combatRadius = ce.Radius - NavigationConstants.CriticalEncounterRadiusPadding;
        if (combatRadius <= 0f)
        {
            return;
        }

        float percent = player.Position.Distance2D(ce.Position) / combatRadius;

        if (percent >= 1.0f)
        {
            Vector3 approach = ce.Position.GetApproachPosition(player.Position, combatRadius * 0.8f, 30f);
            AutoMount.MaybeRemount(config, conditions, objects, approach);

            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new(approach));
            }

            return;
        }

        if (!config.StayMountedWhileWaitingForCe
            && conditions[ConditionFlag.Mounted]
            && EzThrottler.Throttle("WaitingForCriticalEncounter::Unmount")
            && Actions.Unmount.CanCast())
        {
            Actions.Unmount.Cast();
            pathfinder.Stop();
        }
    }
}
