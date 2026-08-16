using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InFateHandler
(
    IAutomatorMemory memory,
    IFateContext context,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    AutoRotationController autoRotation,
    IPlayer playerState,
    AutomatorConfig config,
    ITargetManager targetManager,
    ILogger<InFateHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InFate)
{
    public override StatePriority GetScore()
    {
        if (!memory.TryRemember<GoalMemory>(out GoalMemory goal) || goal.Goal.GoalType is not FateGoal fateGoal)
        {
            return StatePriority.Never;
        }

        return context.GetFateId() == fateGoal.id ? StatePriority.VeryHigh : StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();
        memory.TryAdd(new SuspendTravelForActivityMemory());
        memory.Forget<GoalPathStepMemory>();
        pathfinder.Stop();
        autoRotation.EnableForFate();
        logger.Info("Entered FATE {Id} — travel suspended", context.GetFateId()?.Value.ToString() ?? "?");
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        List<IBattleNpc> fateTargets = context.GetTargets().ToList();
        bool ai = config.CombatAutorotation.UsesCombatAutomation();
        InitialCombatApproachMemory<FateId> approach = GetApproachMemory(context.GetFateId());

        // The AI path used to skip the approach entirely, but we enter InFate the moment we cross
        // the FATE ring — which can be well outside engagement range — and travel is suspended by
        // then. So we walk in ourselves either way, and hand vnav over once we are in range.
        // With AI on, combat starting is not the cue to stop: it may have started at range.
        if (CombatActivityHandler.HandleTargets(
                player,
                playerState,
                fateTargets,
                conditions,
                pathfinder,
                "InFate",
                approach.IsPending,
                stopPathfinderInCombat: !ai,
                deferCombatToBossModAi: ai,
                targetManager: targetManager))
        {
            approach.Complete();
        }
    }

    private InitialCombatApproachMemory<FateId> GetApproachMemory(FateId? fateId)
    {
        if (!memory.TryRemember(out InitialCombatApproachMemory<FateId> approach))
        {
            approach = new();
            memory.TryAdd(approach);
        }

        approach.Track(fateId);
        return approach;
    }
}
