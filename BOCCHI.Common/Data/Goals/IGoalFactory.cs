using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;

namespace BOCCHI.Common.Data.Goals;

public interface IGoalFactory
{
    IGoal Fate(FateId id);

    IGoal CriticalEncounter(CriticalEncounterId id);
}
