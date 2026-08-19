using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;

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
    ILogger<InCriticalEncounterHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InCriticalEncounter)
{
    public override StatePriority GetScore()
    {
        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal)
            || goal.Goal.GoalType is not CriticalEncounterGoal ceGoal)
        {
            return StatePriority.Never;
        }

        // Waiting handed off, or we already entered (EventId can lag while fighting).
        if (TryGetCommittedBattleEncounter(out _))
        {
            return StatePriority.VeryHigh;
        }

        if (context.GetCriticalEncounterId() != ceGoal.id)
        {
            return StatePriority.Never;
        }

        // Combat None: EventId is enough. AI AutoTarget would grab trash on the
        // registration rim — keep walking until we are inside the wait area.
        if (!config.CombatAutorotation.UsesCombatAutomation())
        {
            return StatePriority.VeryHigh;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return StatePriority.Never;
        }

        CriticalEncounter? found = repo.SnapshotWithoutForkedTower().FirstOrDefault(c => c.Id == ceGoal.id);
        if (found is not { } ce || !ce.IsActive())
        {
            return StatePriority.Never;
        }

        float combatRadius = NavigationConstants.CriticalEncounterRedRadius(ce.Radius, ce.AreaShape);
        if (!NavigationConstants.IsInsideCriticalEncounterWaitArea(
                ce.Position,
                combatRadius,
                ce.AreaShape,
                player.Position))
        {
            return StatePriority.Never;
        }

        return StatePriority.VeryHigh;
    }

    public override void Enter()
    {
        base.Enter();
        memory.Forget<WaitingForCriticalEncounterMemory>();
        memory.TryAdd(new SuspendTravelForActivityMemory());
        memory.Forget<GoalPathStepMemory>();
        pathfinder.Stop();
        autoRotation.EnableForCriticalEncounter();
        ushort? ceId = context.GetCriticalEncounterId()?.Value;
        if (ceId == null
            && memory.TryRemember<GoalMemory>(out GoalMemory goal)
            && goal.Goal.GoalType is CriticalEncounterGoal ceGoal)
        {
            ceId = ceGoal.id.Value;
        }

        logger.Info("Entered CE {Id} — travel suspended", ceId?.ToString() ?? "?");
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (config.CombatAutorotation.UsesCombatAutomation())
        {
            DismountAssist.TryDismount(conditions);
            if (!pathfinder.IsIdle())
            {
                pathfinder.Stop();
            }

            return;
        }

        List<IBattleNpc> targets = context.GetTargets().ToList();
        if (targets.Count == 0 && TryGetCommittedBattleEncounter(out CriticalEncounter committed))
        {
            targets = context.GetTargetsFor(committed.Id).ToList();
        }

        CombatActivityHandler.HandleTargets(
            player,
            playerState,
            targets,
            conditions,
            pathfinder,
            "InCriticalEncounter",
            shouldApproachTarget: false);
    }

    private bool TryGetCommittedBattleEncounter(out CriticalEncounter ce)
    {
        ce = null!;

        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal)
            || goal.Goal.GoalType is not CriticalEncounterGoal ceGoal)
        {
            return false;
        }

        CriticalEncounter? found = repo.SnapshotWithoutForkedTower().FirstOrDefault(c => c.Id == ceGoal.id);
        if (found is not { } encounter || !encounter.IsActive())
        {
            return false;
        }

        // Stay committed after enter when EventId / wait-area lag.
        if (memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _))
        {
            ce = encounter;
            return true;
        }

        // Waiting handed off — take CE even if wait-area geometry mismatches.
        if (memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory wait)
            && wait.IsFor(encounter.Id)
            && CriticalEncounterBattleHandoff.IsReady(wait, encounter.Id, context, conditions))
        {
            ce = encounter;
            return true;
        }

        return false;
    }
}
