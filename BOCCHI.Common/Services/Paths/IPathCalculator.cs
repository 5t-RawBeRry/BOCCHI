using BOCCHI.Common.Data.Goals;

namespace BOCCHI.Common.Services.Paths;

public interface IPathCalculator
{
    Task<PathCalculationResult> Calculate(IGoal goal);
}
