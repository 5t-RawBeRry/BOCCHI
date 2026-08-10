using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Ocelot.Chain;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class DeadHandler
(
    IPlayer player,
    IAutomatorMemory memory,
    IPathfinder pathfinder,
    IChainManager chains
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Dead)
{
    public override StatePriority GetScore() =>
        player.Conditions[ConditionFlag.Unconscious] ? StatePriority.Always : StatePriority.Never;

    public override void Enter()
    {
        base.Enter();
        // Stop any in-flight Return so death prompts aren't auto-accepted.
        memory.Forget<ReturningStateMemory>();
        memory.Forget<GoalPathStepMemory>();
        // Cancel pot-chest / travel opens too — PathStep-only cancel left Interact spam while dead.
        chains.CancelAll();
        pathfinder.Stop();
    }

    public override void Handle()
    {
    }
}
