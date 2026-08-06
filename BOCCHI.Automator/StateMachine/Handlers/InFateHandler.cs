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
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Extensions;
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
    ITargetManager targetManager
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.InFate)
{
    private const float DismountDistance = 20f;

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
        // Latch until the FATE goal ends — prevents travel replan / edge stutter with BOCCHI AI.
        memory.TryAdd(new SuspendTravelForActivityMemory());
        memory.Forget<GoalPathStepMemory>();
        pathfinder.Stop();
        autoRotation.EnableForFate();
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        List<IBattleNpc> fateTargets = context.GetTargets().ToList();
        InitialCombatApproachMemory<FateId> approach = GetApproachMemory(context.GetFateId());

        // Stay mounted until within range of a FATE target (or already in combat).
        if (conditions[ConditionFlag.Mounted]
            && (conditions[ConditionFlag.InCombat]
                || (fateTargets.FirstOrDefault() is { } nearest
                    && player.Position.Distance2D(nearest.Position) - nearest.HitboxRadius <= DismountDistance))
            && EzThrottler.Throttle("InFate::Unmount")
            && Actions.Unmount.CanCast())
        {
            Actions.Unmount.Cast();
            pathfinder.Stop();
        }

        if (CombatActivityHandler.HandleTargets(
                player,
                playerState,
                fateTargets,
                conditions,
                pathfinder,
                "InFate",
                approach.IsPending,
                stopPathfinderInCombat: true,
                deferCombatToBossModAi: config.ToggleAiProvider,
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
