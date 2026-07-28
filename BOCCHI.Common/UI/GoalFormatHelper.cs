using BOCCHI.Common.Data.Goals;
using Ocelot.Services.Translation;

namespace BOCCHI.Common.UI;

public static class GoalFormatHelper
{
    public static string Describe(IGoal goal, ITranslator translator)
    {
        return goal.GoalType switch
        {
            FateGoal(var id) => string.Format(translator.T(".goals.fate"), id),
            CriticalEncounterGoal(var id) => string.Format(translator.T(".goals.critical_encounter"), id),
            var _ => goal.Describe()
        };
    }
}
