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
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class InFateHandler
(
    IAutomatorMemory memory,
    IFateContext context,
    IObjectTable objects,
    ICondition conditions,
    IPathfinder pathfinder,
    CombatConfig combat,
    AutomatorConfig automatorConfig,
    ITargetManager targetManager,
    AutoRotationController autoRotation
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
        autoRotation.EnableForActivity();
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        List<IBattleNpc> targets = context.GetTargets().ToList();
        InitialCombatApproachMemory<FateId> approach = GetApproachMemory(context.GetFateId());

        // Stay mounted until within range of a FATE target.
        if (conditions[ConditionFlag.Mounted]
            && targets.FirstOrDefault() is { } nearest
            && player.Position.Distance2D(nearest.Position) - nearest.HitboxRadius <= DismountDistance
            && EzThrottler.Throttle("InFate::Unmount")
            && Actions.Unmount.CanCast())
        {
            Actions.Unmount.Cast();
            pathfinder.Stop();
        }

        if (CombatActivityHandler.HandleTargets(
                player,
                targets,
                combat,
                targetManager,
                conditions,
                pathfinder,
                "InFate",
                approach.IsPending,
                true,
                automatorConfig.ToggleAiProvider))
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
