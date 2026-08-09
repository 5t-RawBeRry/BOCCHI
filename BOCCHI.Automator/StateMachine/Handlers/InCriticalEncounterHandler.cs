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
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Extensions;
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
    ITargetManager targetManager,
    ILogger<InCriticalEncounterHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InCriticalEncounter)
{
    public override StatePriority GetScore()
    {
        if (context.IsInCriticalEncounter())
        {
            return StatePriority.VeryHigh;
        }

        // Player EventId can lag or stay unset while already fighting CE enemies — still enter
        // so BOCCHI AI CE enables (Waiting must not own the whole Battle).
        return TryGetCommittedBattleEncounter(out _) ? StatePriority.VeryHigh : StatePriority.Never;
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

        if (conditions[ConditionFlag.Mounted]
            && EzThrottler.Throttle("InCriticalEncounter::Unmount")
            && Actions.Unmount.CanCast())
        {
            Actions.Unmount.Cast();
            pathfinder.Stop();
        }

        IEnumerable<IBattleNpc> targets = context.GetTargets();
        if (!targets.Any() && TryGetCommittedBattleEncounter(out CriticalEncounter committed))
        {
            targets = context.GetTargetsFor(committed.Id);
        }

        CombatActivityHandler.HandleTargets(
            player,
            playerState,
            targets,
            conditions,
            pathfinder,
            "InCriticalEncounter",
            shouldApproachTarget: false,
            deferCombatToBossModAi: config.ToggleAiProvider,
            targetManager: targetManager);
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

        // Already handed off into InCritical — stay committed for this CE Battle even when
        // EventId / wait-area geometry lag (Unbridled "Return when CE started").
        if (memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _))
        {
            ce = encounter;
            return true;
        }

        // Waiting already decided to hand off — take the CE even if authored wait-area
        // geometry doesn't match the live blue zone (otherwise AI never enables).
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
