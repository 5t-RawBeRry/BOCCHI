using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
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
        PathStepSoftStop.Cancel(chains);
        pathfinder.Stop();
    }

    public override void Handle()
    {
    }
}
