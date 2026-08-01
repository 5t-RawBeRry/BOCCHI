using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services.Goals;

public class GoalValidator
(
    ICriticalEncounterRepository criticalEncounterRepository,
    IFateRepository fateRepository,
    IZoneProvider zones,
    FatesConfig fatesConfig,
    CriticalEncountersConfig criticalEncountersConfig
) : IGoalValidator
{
    public bool Validate(IGoal goal)
    {
        return goal.GoalType switch
        {
            CriticalEncounterGoal(var id) => criticalEncounterRepository.HasCriticalEncounter(id)
                                            && criticalEncountersConfig.IsCriticalEncounterEnabled(id.Value),
            FateGoal(var id) => ValidateFate(id),
            var _ => throw new ArgumentOutOfRangeException(nameof(GoalType))
        };
    }

    private bool ValidateFate(FateId id)
    {
        if (!fateRepository.HasFate(id) || !fatesConfig.IsFateEnabled(id.Value))
        {
            return false;
        }

        if (fatesConfig.MinPotFateMinutesRemaining <= 0 || !zones.GetZone().IsPotFate(id.Value))
        {
            return true;
        }

        Fate? fate = fateRepository.Snapshot().FirstOrDefault(f => f.Id.Value == id.Value);
        if (fate == null)
        {
            return false;
        }

        // Drop pot FATE goals that are about to expire so we don't path into an empty event.
        return fate.TimeRemainingSeconds >= fatesConfig.MinPotFateMinutesRemaining * 60L;
    }
}
