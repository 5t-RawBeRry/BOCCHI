using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using ECommons.Throttlers;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ReturningToJobHandler(IAutomatorMemory memory, ISupportJobFactory jobs, ISupportJobChanger changer) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.ReturningToJob)
{
    public override StatePriority GetScore() => HasJobToRestore() ? StatePriority.AboveNormal : StatePriority.Never;

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

        changer.Change(jobId);
    }

    private bool HasJobToRestore() =>
        memory.TryRemember<BuffSupportJobMemory>(out BuffSupportJobMemory _)
        || memory.TryRemember<TreasureSightSupportJobMemory>(out TreasureSightSupportJobMemory _);

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

        jobId = default;
        return false;
    }

    private void ForgetSavedJobs()
    {
        memory.Forget<BuffSupportJobMemory>();
        memory.Forget<TreasureSightSupportJobMemory>();
    }
}
