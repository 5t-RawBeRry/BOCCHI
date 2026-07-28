using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services.Goals;

public class GoalValidator
(
    ICriticalEncounterRepository criticalEncounterRepository,
    IFateRepository fateRepository
) : IGoalValidator
{
    public bool Validate(IGoal goal)
    {
        return goal.GoalType switch
        {
            CriticalEncounterGoal(var id) => criticalEncounterRepository.HasCriticalEncounter(id),
            FateGoal(var id) => fateRepository.HasFate(id),
            var _ => throw new ArgumentOutOfRangeException(nameof(GoalType))
        };
    }
}
