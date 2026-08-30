using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ReturningToJobHandler
(
    IAutomatorMemory memory,
    ISupportJobFactory jobs,
    ISupportJobChanger changer,
    ICondition conditions
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.ReturningToJob)
{
    // Must beat Pathfinding (High) and Returning's VeryHigh Return latch so job restore is not skipped.
    public override StatePriority GetScore()
    {
        // Only TriagingMemory (active Chemist session) — Pending alone must not block restore
        // after triage clears without entering, and Sight uses CastingTreasureSightMemory.
        if (memory.TryRemember<CastingTreasureSightMemory>(out CastingTreasureSightMemory _)
            || memory.TryRemember<TriagingMemory>(out TriagingMemory _))
        {
            return StatePriority.Never;
        }

        // Critical beats Returning's VeryHigh latch so we restore Chemist/WHM (or Sight) before
        // casting Return — equal VeryHigh ties were registration-order dependent.
        return TryGetJobToRestore(out _) ? StatePriority.Critical : StatePriority.Never;
    }

    public override void Handle()
    {
        if (!EzThrottler.Throttle("ReturningToJobHandler::Gate"))
        {
            return;
        }

        if (!TryGetJobToRestore(out SupportJobId jobId))
        {
            return;
        }

        if (jobs.TryGetCurrent(out SupportJob current) && current.Id == jobId)
        {
            ForgetSavedJobs();
            return;
        }

        if (changer.IsBusy() || PhantomJobChangeGate.IsBlocked(conditions))
        {
            return;
        }

        changer.Change(jobId);
    }

    private bool TryGetJobToRestore(out SupportJobId jobId)
    {
        if (memory.TryRemember<BuffSupportJobMemory>(out BuffSupportJobMemory buffJob))
        {
            jobId = buffJob.Job;
            return true;
        }

        if (memory.TryRemember<TreasureSightSupportJobMemory>(out TreasureSightSupportJobMemory treasureSightJob))
        {
            jobId = treasureSightJob.Job;
            return true;
        }

        if (memory.TryRemember<TriageSupportJobMemory>(out TriageSupportJobMemory triageJob))
        {
            jobId = triageJob.Job;
            return true;
        }

        jobId = default;
        return false;
    }

    private void ForgetSavedJobs()
    {
        memory.Forget<BuffSupportJobMemory>();
        memory.Forget<TreasureSightSupportJobMemory>();
        memory.Forget<TriageSupportJobMemory>();
    }
}
