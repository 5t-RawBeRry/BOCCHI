using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class PathfindingHandler
(
    IAutomatorMemory memory,
    IPathStepExecutor pathStepExecutor,
    IChainManager manager,
    IObjectTable objects,
    IPathfinder pathfinder,
    ILogger<PathfindingHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Pathfinding)
{
    private Task<ChainResult>? currentPathTask;

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);

        // Don't cancel pathing on a same-frame return handoff (restart loop).
        if (next == AutomatorState.Returning)
        {
            currentPathTask = null;
            pathfinder.Stop();
            return;
        }

        ResetPathfinding();
    }

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
        {
            return StatePriority.Never;
        }

        return memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _) ? StatePriority.High : StatePriority.Never;
    }

    public override void Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (!memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory path))
        {
            ResetPathfinding();
            return;
        }

        path.Update();

        if (currentPathTask != null)
        {
            if (currentPathTask.IsCompleted)
            {
                if (currentPathTask.Status == TaskStatus.RanToCompletion)
                {
                    ChainResult result = currentPathTask.Result;
                    if (result.IsSuccess)
                    {
                        logger.Info("Finished current task step...");
                        path.DequeuePathStep();
                    }
                    else
                    {
                        logger.Warning("Path step failed: {Error}", result.ErrorMessage ?? "unknown");
                        pathfinder.Stop();
                    }
                }
                else if (currentPathTask.IsCanceled)
                {
                    logger.Warning("Path step canceled");
                    pathfinder.Stop();
                }
                else
                {
                    logger.Warning("Path step task faulted");
                    pathfinder.Stop();
                }

                currentPathTask = null;
            }

            return;
        }

        if (currentPathTask == null && path.GetNextPathStep() is { } step)
        {
            if (step.PathStepData is Return)
            {
                logger.Info("Handing off return step to ReturningHandler...");
                memory.TryAdd<ReturningStateMemory>();
                path.DequeuePathStep();
                return;
            }

            logger.Info("Starting next task step...");
            currentPathTask = pathStepExecutor.Execute(step);
        }

        if (!path.IsValid)
        {
            memory.Forget<GoalPathStepMemory>();
        }
    }

    private void ResetPathfinding()
    {
        manager.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));

        currentPathTask = null;
        pathfinder.Stop();
    }
}
