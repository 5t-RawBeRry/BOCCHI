using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
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
///     Hold near a CE after arrival. Travel delivers via PathCalculator; this state prevents leaving
///     for another activity while the arrived CE moves from preparation into Battle.
///     Once Battle is underway (player EventId, CE enemies, or combat), yield to
///     <see cref="InCriticalEncounterHandler"/> so BOCCHI AI can enable.
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
    ICriticalEncounterContext context,
    AutomatorConfig config
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.WaitingForCriticalEncounter)
{
    /// <summary>
    ///     If player EventId never appears, still hand off after this so we do not sit in Waiting
    ///     for the whole fight (CE AI never enables).
    /// </summary>
    public override StatePriority GetScore()
    {
        if (!TryGetGoalEncounter(out CriticalEncounter ce))
        {
            return StatePriority.Never;
        }

        bool hasWaitLatch = memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory wait)
                            && wait.IsFor(ce.Id);

        if (ce.IsActive())
        {
            // Hand off to InCritical as soon as we look like participants — do not keep Waiting
            // for the whole Battle when EventId is slow/missing.
            if (ShouldHandOffToInCritical(ce))
            {
                return StatePriority.Never;
            }

            return hasWaitLatch ? StatePriority.VeryHigh : StatePriority.Never;
        }

        if (!ce.IsPreparing())
        {
            return StatePriority.Never;
        }

        // Already arrived — keep Waiting unless we drifted outside the blue zone (wrong LGB / early stop).
        if (hasWaitLatch)
        {
            if (objects.LocalPlayer is { } latchedPlayer)
            {
                float latchedRadius = NavigationConstants.CriticalEncounterRedRadius(ce.Radius);
                if (!NavigationConstants.IsInsideCriticalEncounterWaitArea(
                        ce.Position,
                        latchedRadius,
                        ce.AreaShape,
                        latchedPlayer.Position))
                {
                    memory.Forget<WaitingForCriticalEncounterMemory>();
                    return StatePriority.Never;
                }
            }

            return StatePriority.VeryHigh;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return StatePriority.Never;
        }

        float combatRadius = NavigationConstants.CriticalEncounterRedRadius(ce.Radius);
        if (!NavigationConstants.IsInsideCriticalEncounterWaitArea(
                ce.Position,
                combatRadius,
                ce.AreaShape,
                player.Position))
        {
            return StatePriority.Never;
        }

        // Beat Pathfinding (High) once near the CE.
        return StatePriority.VeryHigh;
    }

    public override void Enter()
    {
        base.Enter();
        StopNavigation();
        memory.Forget<GoalPathStepMemory>();

        if (TryGetGoalEncounter(out CriticalEncounter ce))
        {
            memory.Forget<WaitingForCriticalEncounterMemory>();
            memory.TryAdd(new WaitingForCriticalEncounterMemory(ce.Id));
        }
    }

    public override void Handle()
    {
        if (!memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory wait))
        {
            return;
        }

        if (!TryGetGoalEncounter(out CriticalEncounter ce))
        {
            return;
        }

        if (!wait.IsFor(ce.Id))
        {
            return;
        }

        if (ce.IsActive())
        {
            wait.MarkBattleStarted();
        }
        else if (!ce.IsPreparing())
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

    private bool ShouldHandOffToInCritical(CriticalEncounter ce)
    {
        if (context.GetCriticalEncounterId() == ce.Id)
        {
            return true;
        }

        if (!memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory wait)
            || !wait.IsFor(ce.Id))
        {
            return false;
        }

        return CriticalEncounterBattleHandoff.IsReady(wait, ce.Id, context, conditions);
    }

    private bool TryGetGoalEncounter(out CriticalEncounter ce)
    {
        ce = null!;

        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal)
            || goal.Goal.GoalType is not CriticalEncounterGoal ceGoal)
        {
            return false;
        }

        CriticalEncounter? found = repo.SnapshotWithoutForkedTower().FirstOrDefault(c => c.Id == ceGoal.id);
        if (found is not { } encounter)
        {
            return false;
        }

        ce = encounter;
        return true;
    }

    private void StopNavigation() => PathStepSoftStop.Stop(manager, pathfinder, vnav);
}
