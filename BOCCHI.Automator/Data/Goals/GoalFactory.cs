using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;

namespace BOCCHI.Automator.Data.Goals;

public class GoalFactory : IGoalFactory
{
    public IGoal Fate(FateId id)
    {
        return new Goal
        {
            GoalType = new FateGoal(id),
        };
    }

    public IGoal CriticalEncounter(CriticalEncounterId id)
    {
        return new Goal
        {
            GoalType = new CriticalEncounterGoal(id),
        };
    }
}
