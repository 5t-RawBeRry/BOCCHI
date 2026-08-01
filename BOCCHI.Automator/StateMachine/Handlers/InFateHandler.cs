using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
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

        return context.GetFateId() == fateGoal.id ? StatePriority.High : StatePriority.Never;
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        List<IBattleNpc> targets = context.GetTargets().ToList();

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

        CombatActivityHandler.HandleTargets(
            player,
            targets,
            combat,
            targetManager,
            conditions,
            pathfinder,
            "InFate",
            true
        );
    }
}
