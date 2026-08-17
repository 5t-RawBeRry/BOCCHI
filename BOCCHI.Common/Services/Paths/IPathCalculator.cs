using BOCCHI.Common.Data.Goals;
using System.Numerics;

namespace BOCCHI.Common.Services.Paths;

public interface IPathCalculator
{
    Task<PathCalculationResult> Calculate(IGoal goal);

    /// <summary>
    ///     Plan travel to an arbitrary point, using aethernet hops when they beat walking.
    ///     Pot chest spots for a single FATE are spread over 1600y+ of zone, so walking between
    ///     candidates burned most of the Cache Me window.
    /// </summary>
    Task<PathCalculationResult> CalculateToPosition(Vector3 destination, float arrivalRange);
}
