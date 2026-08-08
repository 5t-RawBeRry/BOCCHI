using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
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
    IZoneProvider zones,
    AutomatorConfig config,
    ICondition conditions,
    AutoRotationController autoRotation,
    ILogger<PathfindingHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.Pathfinding)
{
    private static readonly TimeSpan MountBeforePauseTimeout = TimeSpan.FromSeconds(8);

    private Task<ChainResult>? currentPathTask;

    private string? pendingPauseReason;

    private DateTime mountBeforePauseDeadline = DateTime.MinValue;

    public override void Enter()
    {
        base.Enter();
        // Drop leftover combat target so rotations don't pull trash mid-path.
        targetManager.Target = null;
        autoRotation.DisableForTravel();
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

        if (memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _))
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

        if (pendingPauseReason != null && FinishMountBeforePause())
        {
            return;
        }

        if (!memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory path))
        {
            ResetPathfinding();
            return;
        }

        path.Update();

        // Teleport-only mode: calc produced no Return/Teleport steps → pause for manual.
        if (path.PauseWhenPlanCompletes && path.IsEmptyPlan && currentPathTask == null)
        {
            BeginMountThenPause(TeleportOnlyMessage("no travel steps left"));
            return;
        }

        if (currentPathTask != null)
        {
            // Remount mid-route if Treasure Sight (or anything else) left us on foot.
            if (path.GetNextPathStep()?.PathStepData is Pathfind(var destination, _))
            {
                AutoMount.MaybeRemount(
                    config,
                    conditions,
                    objects,
                    destination,
                    zones.GetZone().IsInBasecamp());
            }

            if (currentPathTask.IsCompleted)
            {
                if (currentPathTask.Status == TaskStatus.RanToCompletion)
                {
                    ChainResult result = currentPathTask.Result;
                    if (result.IsSuccess)
                    {
                        logger.Info("Finished current task step...");
                        PathStepKind completedKind = path.GetNextPathStep()?.Kind ?? PathStepKind.Pathfind;
                        path.DequeuePathStep();

                        if (path.PauseWhenPlanCompletes
                            && path.GetNextPathStep() == null
                            && completedKind is PathStepKind.Teleport or PathStepKind.Return)
                        {
                            currentPathTask = null;
                            memory.Forget<BaseTeleportDelayMemory>();
                            BeginMountThenPause(TeleportOnlyMessage("arrived at aetheryte"));
                            return;
                        }
                    }
                    else if (result.IsCanceled)
                    {
                        PauseForManualPathing("Path step canceled — pausing navigation until Illegal Mode is toggled");
                        return;
                    }
                    else
                    {
                        logger.Warning("Path step failed: {Error}", result.ErrorMessage ?? "unknown");
                        pathfinder.Stop();
                        path.DequeuePathStep();
                    }
                }
                else if (currentPathTask.IsCanceled)
                {
                    PauseForManualPathing("Path step canceled — pausing navigation until Illegal Mode is toggled");
                    return;
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

                if (path.PauseWhenPlanCompletes && path.GetNextPathStep() == null)
                {
                    BeginMountThenPause(TeleportOnlyMessage("returned to camp"));
                }

                return;
            }

            if (step.PathStepData is Teleport
                && zones.GetZone().IsInBasecamp()
                && !WaitForBaseTeleportDelay())
            {
                return;
            }

            logger.Info("Starting next task step...");
            memory.Forget<BaseTeleportDelayMemory>();
            currentPathTask = pathStepExecutor.Execute(step);
            return;
        }

        // Empty plan (already at destination) — keep GoalPathStepMemory so Automator doesn't recreate.
        if (!path.IsValid)
        {
            memory.Forget<GoalPathStepMemory>();
        }
    }

    /// <returns>False while still waiting; true when ready to teleport.</returns>
    private bool WaitForBaseTeleportDelay()
    {
        if (config.MaxBaseTeleportDelaySeconds <= 0)
        {
            return true;
        }

        if (!memory.TryRemember<BaseTeleportDelayMemory>(out BaseTeleportDelayMemory delay))
        {
            delay = new BaseTeleportDelayMemory(BaseTeleportDelay.Roll(config));
            if (delay.Delay <= TimeSpan.Zero)
            {
                return true;
            }

            memory.TryAdd(delay);
            logger.Info("Waiting {Seconds:F1}s at camp before teleport", delay.Delay.TotalSeconds);
            return false;
        }

        return delay.IsReady();
    }

    private void BeginMountThenPause(string reason)
    {
        if (!config.ShouldAutoMount || conditions[ConditionFlag.Mounted])
        {
            PauseForManualPathing(reason);
            return;
        }

        pendingPauseReason = reason;
        mountBeforePauseDeadline = DateTime.UtcNow + MountBeforePauseTimeout;
        if (!conditions[ConditionFlag.Mounting])
        {
            MountWait.TryCast(config.PreferredMountId);
        }
    }

    /// <returns>True when this frame handled the mount-before-pause wait.</returns>
    private bool FinishMountBeforePause()
    {
        if (pendingPauseReason == null)
        {
            return false;
        }

        if (conditions[ConditionFlag.Mounted]
            || !config.ShouldAutoMount
            || DateTime.UtcNow >= mountBeforePauseDeadline)
        {
            string reason = pendingPauseReason;
            pendingPauseReason = null;
            PauseForManualPathing(reason);
            return true;
        }

        if (!conditions[ConditionFlag.Mounting]
            && EzThrottler.Throttle("Pathfinding::MountBeforePause", 750))
        {
            MountWait.TryCast(config.PreferredMountId);
        }

        return true;
    }

    private static string TeleportOnlyMessage(string where) =>
        $"Stop after return and teleport: {where} — paused so you can walk the rest "
        + "(Illegal Mode → Stop after return and teleport; toggle Illegal Mode to resume)";

    private void PauseForManualPathing(string reason)
    {
        logger.Info("{Reason} (toggle Illegal Mode to resume)", reason);
        pathfinder.Stop();
        ResetPathfinding();
        memory.Forget<GoalPathStepMemory>();
        memory.Forget<GoalMemory>();
        memory.Forget<BaseTeleportDelayMemory>();
        memory.TryAdd<NavigationInterruptedMemory>();
    }

    private void ResetPathfinding()
    {
        manager.CancelWhere(name => name.StartsWith("PathStep::", StringComparison.Ordinal));

        currentPathTask = null;
        pendingPauseReason = null;
        pathfinder.Stop();
    }
}
