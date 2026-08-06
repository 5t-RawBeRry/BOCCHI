using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;
using System.Numerics;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InCriticalEncounterHandler
(
    IAutomatorMemory memory,
    ICriticalEncounterContext context,
    ICriticalEncounterRepository repo,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    AutoRotationController autoRotation,
    IPlayer playerState,
    AutomatorConfig config,
    ITargetManager targetManager
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InCriticalEncounter)
{
    public override StatePriority GetScore() => context.IsInCriticalEncounter() ? StatePriority.VeryHigh : StatePriority.Never;

    public override void Enter()
    {
        base.Enter();
        memory.Forget<WaitingForCriticalEncounterMemory>();
        memory.TryAdd(new SuspendTravelForActivityMemory());
        memory.Forget<GoalPathStepMemory>();
        pathfinder.Stop();
        autoRotation.EnableForCriticalEncounter();
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (conditions[ConditionFlag.Mounted]
            && EzThrottler.Throttle("InCriticalEncounter::Unmount")
            && Actions.Unmount.CanCast())
        {
            Actions.Unmount.Cast();
            pathfinder.Stop();
        }

        // Still outside the combat (red) ring — walk in before combat AI / targets own movement.
        // Waiting stops when Battle starts; without this, empty GetTargets() freezes you outside
        // ("take to the field" / #140 follow-up, Discord Texas).
        if (TryPathIntoCombatRing(player))
        {
            return;
        }

        InitialCombatApproachMemory<CriticalEncounterId> approach =
            GetApproachMemory(context.GetCriticalEncounterId());

        if (CombatActivityHandler.HandleTargets(
                player,
                playerState,
                context.GetTargets(),
                conditions,
                pathfinder,
                "InCriticalEncounter",
                approach.IsPending,
                deferCombatToBossModAi: config.ToggleAiProvider,
                targetManager: targetManager))
        {
            approach.Complete();
        }
    }

    private bool TryPathIntoCombatRing(Dalamud.Game.ClientState.Objects.Types.IGameObject player)
    {
        CriticalEncounterId? id = context.GetCriticalEncounterId();
        if (id == null)
        {
            return false;
        }

        CriticalEncounter? ce = repo.SnapshotWithoutForkedTower().FirstOrDefault(c => c.Id == id);
        if (ce == null)
        {
            return false;
        }

        float combatRadius = NavigationConstants.CriticalEncounterRedRadius(ce.Radius);
        if (combatRadius <= 0f)
        {
            return false;
        }

        float holdRadius = CriticalEncounterWaitProfiles.HoldRadius(combatRadius, ce.Id.Value);
        float dist = player.Position.Distance2D(ce.Position);
        if (dist <= holdRadius)
        {
            return false;
        }

        Vector3 approach = NavigationApproach.GetCriticalEncounterApproachPosition(
            ce.Position,
            player.Position,
            combatRadius,
            ce.Id.Value);

        if (pathfinder.IsIdle()
            && EzThrottler.Throttle("InCriticalEncounter::EnterRing", 500))
        {
            pathfinder.PathfindAndMoveTo(new PathfinderConfig(approach)
            {
                DistanceThreshold = 1.5f,
                ShouldSnapToFloor = true,
            });
        }

        return true;
    }

    private InitialCombatApproachMemory<CriticalEncounterId> GetApproachMemory(CriticalEncounterId? encounterId)
    {
        if (!memory.TryRemember(out InitialCombatApproachMemory<CriticalEncounterId> approach))
        {
            approach = new();
            memory.TryAdd(approach);
        }

        approach.Track(encounterId);
        return approach;
    }
}
