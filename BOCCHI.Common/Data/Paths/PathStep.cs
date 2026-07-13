using System.Numerics;
using BOCCHI.Common.Services.Paths;
using Ocelot.Chain;

namespace BOCCHI.Common.Data.Paths;

public interface IPathStep
{
    PathStepType PathStepData { get; }

    PathStepKind Kind { get; init; }

    string Describe();

    bool TryExecute(IPathStepExecutor executor);
}

public abstract record PathStepType;

public sealed record Teleport(uint id) : PathStepType;

public sealed record Pathfind(Vector3 destination, float range) : PathStepType;

public sealed record Return : PathStepType;

public enum PathStepKind
{
    Teleport,
    Pathfind,
    Return,
}

public class PathStep : IPathStep
{
    private Task<ChainResult>? task = null;

    public required PathStepType PathStepData { get; init; }

    public required PathStepKind Kind { get; init; }

    public string Describe()
    {
        return PathStepData switch
        {
            Pathfind(var destination, var range) => $"Pathfind to {destination:f2} (range = {range:f2})",
            Teleport(var id) => $"Teleport to {id}",
            Return _ => "Return to Basecamp",
            _ => throw new ArgumentOutOfRangeException(nameof(PathStepData))
        };
    }

    public bool TryExecute(IPathStepExecutor executor)
    {
        if (task != null)
        {
            if (task.IsCompleted)
            {
                task.Dispose();
                task = null;
                return true;
            }

            return false;
        }

        task = executor.Execute(this);
        return false;
    }

    public static PathStep Teleport(uint id)
    {
        return new PathStep
        {
            PathStepData = new Teleport(id),
            Kind = PathStepKind.Teleport,
        };
    }

    public static PathStep Pathfind(Vector3 destination, float range = 0f)
    {
        return new PathStep
        {
            PathStepData = new Pathfind(destination, range),
            Kind = PathStepKind.Pathfind,
        };
    }

    public static PathStep Return()
    {
        return new PathStep
        {
            PathStepData = new Return(),
            Kind = PathStepKind.Return,
        };
    }
}
