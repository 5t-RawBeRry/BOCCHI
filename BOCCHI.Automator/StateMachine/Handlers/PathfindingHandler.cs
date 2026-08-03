using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Game.ClientState.Conditions;
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
    ITargetManager targetManager,
    AutomatorConfig config,
    ICondition conditions,
    MechanicAiController mechanicAi,
    ILogger<PathfindingHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Pathfinding)
{
    private Task<ChainResult>? currentPathTask;

    public override void Enter()
    {
        base.Enter();
        // Drop any leftover combat target so rotations don't pull trash mid-path.
        targetManager.Target = null;
        mechanicAi.DisableForTravel();
    }

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
            // Remount mid-route if Treasure Sight (or anything else) left us on foot.
            if (path.GetNextPathStep()?.PathStepData is Pathfind(var destination, _))
            {
                AutoMount.MaybeRemount(config, conditions, objects, destination);
            }

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
                    else if (result.IsCanceled)
                    {
                        // User stop / soft interrupt — pause auto-nav so we don't immediately
                        // rebuild the same Return+teleport chain (retry spam). Toggle Illegal Mode to resume.
                        logger.Info("Path step canceled — pausing navigation until Illegal Mode is toggled");
                        pathfinder.Stop();
                        memory.Forget<GoalPathStepMemory>();
                        memory.Forget<GoalMemory>();
                        memory.TryAdd<NavigationInterruptedMemory>();
                    }
                    else
                    {
                        logger.Warning("Path step failed: {Error}", result.ErrorMessage ?? "unknown");
                        pathfinder.Stop();
                        // Drop failed step; remaining plan continues (or empties → replan from here).
                        path.DequeuePathStep();
                    }
                }
                else if (currentPathTask.IsCanceled)
                {
                    logger.Warning("Path step canceled — pausing navigation until Illegal Mode is toggled");
                    pathfinder.Stop();
                    memory.Forget<GoalPathStepMemory>();
                    memory.Forget<GoalMemory>();
                    memory.TryAdd<NavigationInterruptedMemory>();
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
                memory.TryAdd(new ReturningStateMemory(ReturnDelay.Roll(config)));
                path.DequeuePathStep();
                return;
            }

            logger.Info("Starting next task step...");
            currentPathTask = pathStepExecutor.Execute(step);
            return;
        }

        // Empty plan (already at destination) — keep GoalPathStepMemory so Automator doesn't recreate (#92).
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
