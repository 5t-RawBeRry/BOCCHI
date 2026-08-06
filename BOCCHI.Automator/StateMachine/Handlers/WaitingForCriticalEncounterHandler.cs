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
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

/// <summary>
///     Hold near a preparing CE until Battle. Travel delivers via PathCalculator; this state never vnavs.
/// </summary>
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

        if (!TryGetPreparingGoal(out CriticalEncounter ce))
        {
            return StatePriority.Never;
        }

        float waitRadius = MathF.Max(1f, ce.Radius);
        if (player.Position.Distance2D(ce.Position) >= waitRadius)
        {
            return StatePriority.Never;
        }

        // Beat Pathfinding (High) once near the CE (#132).
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
        if (!memory.TryRemember<WaitingForCriticalEncounterMemory>(out _))
        {
            return;
        }

        if (!TryGetPreparingGoal(out _))
        {
            return;
        }

        StopNavigation();

        if (!config.StayMountedWhileWaitingForCe
            && conditions[ConditionFlag.Mounted]
            && EzThrottler.Throttle("WaitingForCriticalEncounter::Unmount")
            && Actions.Unmount.CanCast())
        {
            Actions.Unmount.Cast();
        }
    }

    private bool TryGetPreparingGoal(out CriticalEncounter ce)
    {
        ce = null!;

        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal)
            || goal.Goal.GoalType is not CriticalEncounterGoal ceGoal)
        {
            return false;
        }

        CriticalEncounter? found = repo.SnapshotWithoutForkedTower().FirstOrDefault(c => c.Id == ceGoal.id);
        if (found is not { } preparing || !preparing.IsPreparing())
        {
            return false;
        }

        ce = preparing;
        return true;
    }

    private void StopNavigation()
    {
        manager.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));
        pathfinder.Stop();
        vnav.Stop();
    }
}
