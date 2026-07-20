using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services.Paths;
using Ocelot.Services.Logger;
using System.Numerics;
namespace BOCCHI.Common.Data.StateMemory;

public sealed class ApplyingBuffsMemory;
public sealed class CastingTreasureSightMemory;
public class WaitingForCriticalEncounterMemory;

public sealed class GoalMemory(IGoal goal)
{
    public IGoal Goal { get; } = goal;
}

public sealed class IdleStateMemory
{
    public DateTimeOffset Entered { get; } = DateTimeOffset.UtcNow;

    public int ApproachCandidateIndex { get; set; }

    public TimeSpan GetIdleTime() => DateTimeOffset.UtcNow - Entered;
}

public sealed class ReturningStateMemory
{
    public DateTimeOffset QueuedAt { get; } = DateTimeOffset.UtcNow;

    public TimeSpan GetTimeQueued() => DateTimeOffset.UtcNow - QueuedAt;
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
    public FateId FateId { get; } = fateId;

    public Queue<Vector3> Chests { get; } = new(chestPositions);

    public int TotalChests { get; } = chestPositions.Count();

    public int RemainingChests => Chests.Count;
}

public sealed class GoalPathStepMemory(IGoal goal, IPathCalculator calculator, ILogger logger)
{
    private Task<Queue<IPathStep>>? pathStepTask = calculator.Calculate(goal);

    public Queue<IPathStep> PathSteps { get; private set; } = [];

    public bool IsValid => pathStepTask != null || PathSteps.Count != 0;

    public void Update()
    {
        if (pathStepTask == null)
        {
            return;
        }

        if (!pathStepTask.IsCompleted)
        {
            logger.Info("Pathstep running...");
            return;
        }

        if (pathStepTask.IsCompletedSuccessfully)
        {
            PathSteps = pathStepTask.Result;
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
