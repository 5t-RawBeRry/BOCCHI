using BOCCHI.Common.Data.Paths;

namespace BOCCHI.Common.Services.Paths;

/// <summary>Outcome of planning travel to a FATE/CE goal.</summary>
public readonly record struct PathCalculationResult(Queue<IPathStep> Steps, bool RoutingFailed = false)
{
    /// <summary>Already at / inside the goal, or no travel required.</summary>
    public static PathCalculationResult NoTravelNeeded() => new([]);

    /// <summary>Calculators produced no route while still far from the goal.</summary>
    public static PathCalculationResult Failed() => new([], RoutingFailed: true);

    public static PathCalculationResult Planned(IEnumerable<IPathStep> steps) => new(new Queue<IPathStep>(steps));
}
