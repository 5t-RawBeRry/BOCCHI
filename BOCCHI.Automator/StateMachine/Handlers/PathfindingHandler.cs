using BOCCHI.Automator.Data;
using BOCCHI.Automator.Data.StateMemory;
using BOCCHI.Automator.Services;
using BOCCHI.Automator.Services.Paths;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.UI;
using Ocelot.States.Score;
using Action = System.Action;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class PathfindingHandler(
    IChainManager manager,
    IAutomatorMemory memory,
    IPathStepExecutor pathStepExecutor,
    IObjectTable objects,
    ICondition conditions,
    ILogger<PathfindingHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Pathfinding)
{
    private Task<ChainResult>? currentPathTask;

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<ApplyingBuffsMemory>(out var _))
        {
            return StatePriority.Never;
        }

        return memory.TryRemember<GoalPathStepMemory>(out var _) ? StatePriority.High : StatePriority.Never;
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (!memory.TryRemember<GoalPathStepMemory>(out var path))
        {
            return;
        }

        path.Update();

        if (currentPathTask != null)
        {
            if (currentPathTask.IsCompleted)
            {
                if (currentPathTask.IsCompletedSuccessfully)
                {
                    logger.Info("Finished current task step...");
                    path.DequeuePathStep();
                }

                logger.Info("Disposing of path task");
                currentPathTask.Dispose();
                currentPathTask = null;
            }

            return;
        }

        if (currentPathTask == null && path.GetNextPathStep() is { } step)
        {
            logger.Info("Starting next task step...");
            currentPathTask = pathStepExecutor.Execute(step);
        }

        if (!path.IsValid)
        {
            memory.Forget<GoalPathStepMemory>();
        }
    }
}
