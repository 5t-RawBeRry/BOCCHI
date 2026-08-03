using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using ECommons.Throttlers;
using Ocelot.Services.Logger;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

/// <summary>
///     When the current phantom job is maxed, switch to the next unlocked non-maxed job (#89).
/// </summary>
public class LevelingPhantomJobHandler
(
    IAutomatorMemory memory,
    ISupportJobFactory jobs,
    ISupportJobChanger changer,
    AutomatorConfig config,
    ILogger<LevelingPhantomJobHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.LevelingPhantomJob)
{
    public override StatePriority GetScore()
    {
        if (!config.PhantomJobsLevelingMode)
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<CastingTreasureSightMemory>(out CastingTreasureSightMemory _)
            || memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _)
            || memory.TryRemember<BuffSupportJobMemory>(out BuffSupportJobMemory _)
            || memory.TryRemember<TreasureSightSupportJobMemory>(out TreasureSightSupportJobMemory _)
            || memory.TryRemember<GoalMemory>(out GoalMemory _)
            || memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _)
            || memory.TryRemember<NavigationInterruptedMemory>(out NavigationInterruptedMemory _))
        {
            return StatePriority.Never;
        }

        if (!jobs.TryGetCurrent(out SupportJob current) || !IsMaxed(current))
        {
            return StatePriority.Never;
        }

        return TryFindNextJob(current, out _) ? StatePriority.Normal : StatePriority.Never;
    }

    public override void Handle()
    {
        if (!EzThrottler.Throttle("LevelingPhantomJobHandler::Gate", 1000))
        {
            return;
        }

        if (changer.IsBusy())
        {
            return;
        }

        if (!jobs.TryGetCurrent(out SupportJob current) || !IsMaxed(current))
        {
            return;
        }

        if (!TryFindNextJob(current, out SupportJobId next))
        {
            return;
        }

        logger.Info("Phantom job {Current} is maxed — switching to {Next}", current.Id, next);
        changer.Change(next);
    }

    private bool TryFindNextJob(SupportJob current, out SupportJobId next)
    {
        next = default;
        List<SupportJob> ordered = jobs.All().OrderBy(job => (int)job.Id).ToList();
        if (ordered.Count == 0)
        {
            return false;
        }

        int start = ordered.FindIndex(job => job.Id == current.Id);
        if (start < 0)
        {
            start = 0;
        }

        for (int offset = 1; offset <= ordered.Count; offset++)
        {
            SupportJob candidate = ordered[(start + offset) % ordered.Count];
            if (candidate.Id == current.Id)
            {
                continue;
            }

            if (candidate.Level > 0 && candidate.Level < candidate.Data.LevelMax)
            {
                next = candidate.Id;
                return true;
            }
        }

        return false;
    }

    private static bool IsMaxed(SupportJob job) =>
        job.Data.LevelMax > 0 && job.Level >= job.Data.LevelMax;
}
