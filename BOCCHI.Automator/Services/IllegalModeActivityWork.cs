using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services;

/// <summary>Shared Illegal Mode activity / job-restore checks for Triage, Sight, and buffs.</summary>
internal static class IllegalModeActivityWork
{
    /// <summary>
    ///     FATE/CE travel and participation. Excludes pot chests, buffs, and Sight
    ///     (those are not "raise after activity" moments).
    /// </summary>
    public static bool HasPrimaryActivity(IAutomatorMemory memory) =>
        memory.TryRemember<GoalMemory>(out GoalMemory _)
        || memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory _)
        || memory.TryRemember<WaitingForPotFateMemory>(out WaitingForPotFateMemory _)
        || memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _)
        || memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _)
        || memory.TryRemember<CommittedCriticalEncounterMemory>(out CommittedCriticalEncounterMemory _);

    /// <summary>Anything that should keep the treasure filler from surveying / hunting.</summary>
    public static bool HasFillerBlockingActivity(IAutomatorMemory memory) =>
        HasPrimaryActivity(memory)
        || memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
        || memory.TryRemember<PendingPotChestFarmMemory>(out PendingPotChestFarmMemory _)
        || memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _)
        || memory.TryRemember<CastingTreasureSightMemory>(out CastingTreasureSightMemory _)
        || TriageSession.IsActive(memory);

    public static bool HasPendingJobRestore(IAutomatorMemory memory) =>
        TryGetPendingJobRestore(memory, out _);

    public static bool TryGetPendingJobRestore(IAutomatorMemory memory, out SupportJobId jobId)
    {
        if (memory.TryRemember<BuffSupportJobMemory>(out BuffSupportJobMemory buff))
        {
            jobId = buff.Job;
            return true;
        }

        if (memory.TryRemember<TreasureSightSupportJobMemory>(out TreasureSightSupportJobMemory sight))
        {
            jobId = sight.Job;
            return true;
        }

        if (memory.TryRemember<TriageSupportJobMemory>(out TriageSupportJobMemory triage))
        {
            jobId = triage.Job;
            return true;
        }

        jobId = default;
        return false;
    }

    public static void ForgetJobRestoreMemories(IAutomatorMemory memory)
    {
        memory.Forget<BuffSupportJobMemory>();
        memory.Forget<TreasureSightSupportJobMemory>();
        memory.Forget<TriageSupportJobMemory>();
    }

    /// <summary>
    ///     Clears restore latches that match the current job, and drops a Freelancer latch while
    ///     already on a real combat job (Freelancer is only for Inquiring Mind / Sight casts).
    /// </summary>
    public static bool TryClearCompletedJobRestore(IAutomatorMemory memory, ISupportJobFactory jobs)
    {
        if (!HasPendingJobRestore(memory))
        {
            return true;
        }

        if (!jobs.TryGetCurrent(out SupportJob current))
        {
            return false;
        }

        // Freelancer is never a restore destination — drop a stale Freelancer latch once we are
        // back on any other job. Do NOT treat Knight/Bard/Monk/Dancer as temporary: those are
        // valid mains and also crystal-buff casters (otherwise Freelancer sticks after refresh).
        if (current.Id != SupportJobId.PhantomFreelancer)
        {
            ForgetIfBuffSwapTarget<BuffSupportJobMemory>(memory, m => m.Job);
            ForgetIfBuffSwapTarget<TreasureSightSupportJobMemory>(memory, m => m.Job);
        }

        ForgetIfMatchesCurrent<BuffSupportJobMemory>(memory, current.Id, m => m.Job);
        ForgetIfMatchesCurrent<TreasureSightSupportJobMemory>(memory, current.Id, m => m.Job);
        ForgetIfMatchesCurrent<TriageSupportJobMemory>(memory, current.Id, m => m.Job);

        return !HasPendingJobRestore(memory);
    }

    /// <summary>
    ///     Jobs used only as temporary cast vehicles — never a restore destination.
    ///     Crystal buffs also use Knight/Bard/Monk/Dancer, but those are valid mains and must
    ///     remain latchable (see <see cref="TryRememberPreBuffJob"/>).
    /// </summary>
    public static bool IsBuffSwapJob(SupportJobId id) =>
        id is SupportJobId.PhantomFreelancer;

    /// <summary>Latch the current combat job once before the buff SM starts swapping.</summary>
    public static bool TryRememberPreBuffJob(IAutomatorMemory memory, ISupportJobFactory jobs)
    {
        if (memory.TryRemember<BuffSupportJobMemory>(out _))
        {
            return false;
        }

        if (!jobs.TryGetCurrent(out SupportJob current) || IsBuffSwapJob(current.Id))
        {
            return false;
        }

        return memory.TryAdd(new BuffSupportJobMemory(current.Id));
    }

    /// <summary>
    ///     Drop wait/path latches (not GoalMemory). Soft-suspend / goal-abort / path refresh.
    /// </summary>
    public static void ForgetTravelLatches(IAutomatorMemory memory, bool includePotChests = false)
    {
        memory.Forget<GoalPathStepMemory>();
        memory.Forget<WaitingForCriticalEncounterMemory>();
        memory.Forget<WaitingForPotFateMemory>();
        memory.Forget<SuspendTravelForActivityMemory>();
        memory.Forget<CommittedCriticalEncounterMemory>();
        if (includePotChests)
        {
            memory.Forget<PotChestFarmMemory>();
        }
    }

    private static void ForgetIfMatchesCurrent<T>(
        IAutomatorMemory memory,
        SupportJobId current,
        Func<T, SupportJobId> job) where T : class
    {
        if (memory.TryRemember(out T saved) && job(saved) == current)
        {
            memory.Forget<T>();
        }
    }

    private static void ForgetIfBuffSwapTarget<T>(
        IAutomatorMemory memory,
        Func<T, SupportJobId> job) where T : class
    {
        if (memory.TryRemember(out T saved) && IsBuffSwapJob(job(saved)))
        {
            memory.Forget<T>();
        }
    }
}

/// <summary>Pending / active Triage Mode session flags.</summary>
internal static class TriageSession
{
    public static bool IsActive(IAutomatorMemory memory) =>
        memory.TryRemember<PendingTriageMemory>(out PendingTriageMemory _)
        || memory.TryRemember<TriagingMemory>(out TriagingMemory _);

    public static void Clear(IAutomatorMemory memory)
    {
        memory.Forget<PendingTriageMemory>();
        memory.Forget<TriagingMemory>();
    }
}
