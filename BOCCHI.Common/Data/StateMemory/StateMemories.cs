using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services.Paths;
using System.Numerics;

namespace BOCCHI.Common.Data.StateMemory;

public sealed class ApplyingBuffsMemory;

public sealed class ManualBuffRunMemory;

public sealed class CastingTreasureSightMemory;
public class WaitingForCriticalEncounterMemory;

/// <summary>
///     User / soft-cancel stopped navigation. Blocks auto-replan until Illegal Mode is toggled.
/// </summary>
public sealed class NavigationInterruptedMemory;

/// <summary>
///     One initial combat approach per FATE/CE. Re-arms when the activity id changes.
/// </summary>
public sealed class InitialCombatApproachMemory<TActivityId>
    where TActivityId : struct
{
    private TActivityId? activityId;

    public bool IsPending { get; private set; }

    public void Track(TActivityId? currentActivityId)
    {
        if (Nullable.Equals(activityId, currentActivityId))
        {
            return;
        }

        activityId = currentActivityId;
        IsPending = currentActivityId.HasValue;
    }

    public void Complete()
    {
        IsPending = false;
    }
}

public sealed class GoalMemory(IGoal goal)
{
    public IGoal Goal
    {
        get => goal;
    }
}

public sealed class IdleStateMemory(TimeSpan returnAfter)
{
    public readonly DateTimeOffset Entered = DateTimeOffset.UtcNow;

    /// <summary>Rolled wait (2..max) before opportunistic Return while idle.</summary>
    public readonly TimeSpan ReturnAfter = returnAfter;

    public int ApproachCandidateIndex { get; set; }

    public TimeSpan GetIdleTime() => DateTimeOffset.UtcNow - Entered;

    public bool IsReadyToReturn() => GetIdleTime() >= ReturnAfter;
}

public sealed class ReturningStateMemory(TimeSpan castDelay)
{
    public readonly DateTimeOffset QueuedAt = DateTimeOffset.UtcNow;

    /// <summary>Rolled wait before casting Return (path handoff after FATE/CE). Zero when already waited while idle.</summary>
    public readonly TimeSpan CastDelay = castDelay;

    public TimeSpan GetTimeQueued() => DateTimeOffset.UtcNow - QueuedAt;

    public bool IsReadyToCast() => GetTimeQueued() >= CastDelay;
}

public class BuffSupportJobMemory(SupportJobId job)
{
    public readonly SupportJobId Job = job;
}

public class TreasureSightSupportJobMemory(SupportJobId job)
{
    public readonly SupportJobId Job = job;
}

public sealed class PotChestFarmMemory(FateId fateId, IEnumerable<Vector3> chestPositions)
{
    public FateId FateId
    {
        get => fateId;
    }

    public readonly Queue<Vector3> Chests = new(chestPositions);

    public readonly int TotalChests = chestPositions.Count();

    public int RemainingChests => Chests.Count;

    /// <summary>When we started waiting for the current (peek) chest to spawn.</summary>
    public DateTimeOffset WaitingForSpawnSince { get; set; } = DateTimeOffset.MinValue;
}

public sealed class GoalPathStepMemory(IGoal goal, IPathCalculator calculator)
{
    private Task<Queue<IPathStep>>? pathStepTask = calculator.Calculate(goal);

    /// <summary>Calc finished with no steps (already at destination). Keeps memory valid so Automator doesn't recreate an empty plan every tick.</summary>
    private bool emptyPlan;

    public Queue<IPathStep> PathSteps { get; private set; } = [];

    public bool IsValid => pathStepTask != null || PathSteps.Count != 0 || emptyPlan;

    public void Update()
    {
        if (pathStepTask == null)
        {
            return;
        }

        if (!pathStepTask.IsCompleted)
        {
            return;
        }

        if (pathStepTask.IsCompletedSuccessfully)
        {
            PathSteps = pathStepTask.Result;
            emptyPlan = PathSteps.Count == 0;
        }

        pathStepTask = null;
    }

    public IPathStep? GetNextPathStep() => PathSteps.Count > 0 && PathSteps.TryPeek(out IPathStep? step) ? step : null;

    public void DequeuePathStep()
    {
        if (PathSteps.Any())
        {
            PathSteps.Dequeue();
        }
    }
}
