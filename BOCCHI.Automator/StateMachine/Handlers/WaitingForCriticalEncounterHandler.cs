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
using Ocelot.Ipc.VNavmesh;
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
    IVNavmeshIpc vnav,
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

        // Beat Pathfinding (High) while walking the last stretch into registration (#132).
        return StatePriority.VeryHigh;
    }

    public override void Enter()
    {
        base.Enter();
        StopNavigation();
        memory.Forget<GoalPathStepMemory>();
        memory.TryAdd(new WaitingForCriticalEncounterMemory());
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

        if (!memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory wait))
        {
            return;
        }

        float percent = player.Position.Distance2D(ce.Position) / combatRadius;

        // Inside the blue box — hold wherever we are; don't yank back to the walk-in target.
        if (percent <= NavigationConstants.CriticalEncounterRegistrationMaxRatio)
        {
            wait.HoldingPosition = true;
            StopNavigation();

            if (!config.StayMountedWhileWaitingForCe
                && conditions[ConditionFlag.Mounted]
                && EzThrottler.Throttle("WaitingForCriticalEncounter::Unmount")
                && Actions.Unmount.CanCast())
            {
                Actions.Unmount.Cast();
            }

            return;
        }

        wait.HoldingPosition = false;

        // Outside registration but still in CE wait range — walk in once, then hold above.
        float approachRange = combatRadius * NavigationConstants.CriticalEncounterWaitApproachRatio;
        Vector3 approach = ce.Position.GetApproachPosition(player.Position, approachRange);

        if (pathfinder.IsIdle())
        {
            pathfinder.PathfindAndMoveTo(new PathfinderConfig(approach)
            {
                DistanceThreshold = 1.5f,
                ShouldSnapToFloor = true,
            });
        }

        AutoMount.MaybeRemount(config, conditions, objects, approach);
    }

    private void StopNavigation()
    {
        manager.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));
        pathfinder.Stop();
        vnav.Stop();
    }
}
