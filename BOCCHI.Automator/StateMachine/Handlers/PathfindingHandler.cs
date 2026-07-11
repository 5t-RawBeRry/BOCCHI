using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Automator.Services.Paths;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Services.Logger;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class PathfindingHandler(
    IAutomatorMemory memory,
    IPathStepExecutor pathStepExecutor,
    IObjectTable objects,
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
