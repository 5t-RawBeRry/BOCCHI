using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services.Goals;

public class GoalValidator
(
    ICriticalEncounterRepository criticalEncounterRepository,
    ICriticalEncounterContext criticalEncounterContext,
    IFateRepository fateRepository,
    IZoneProvider zones,
    FatesConfig fatesConfig,
    CriticalEncountersConfig criticalEncountersConfig,
    IPotCycleTracker potCycle
) : IGoalValidator
{
    public bool Validate(IGoal goal)
    {
        return goal.GoalType switch
        {
            CriticalEncounterGoal(var id) => ValidateCriticalEncounter(id),
            FateGoal(var id) => ValidateFate(id),
            var _ => throw new ArgumentOutOfRangeException(nameof(GoalType))
        };
    }

    private bool ValidateCriticalEncounter(CriticalEncounterId id)
    {
        if (!criticalEncountersConfig.IsCriticalEncounterEnabled(id.Value))
        {
            return false;
        }

        CriticalEncounter? ce = criticalEncounterRepository.SnapshotWithoutForkedTower()
            .FirstOrDefault(c => c.Id == id);
        if (ce == null)
        {
            return false;
        }

        // Keep while Register/Warmup. During Battle only if we're already inside this CE —
        // can't join from outside, so drop the goal and pick something else.
        return ce.IsPreparing()
               || (ce.IsActive() && criticalEncounterContext.GetCriticalEncounterId() == id);
    }

    private bool ValidateFate(FateId id)
    {
        if (!fateRepository.HasFate(id) || !fatesConfig.IsFateEnabled(id.Value))
        {
            return false;
        }

        bool isPot = zones.GetZone().IsPotFate(id.Value);
        if (!isPot)
        {
            PotCycleSnapshot cycle = potCycle.Snapshot;
            bool potFarming = fatesConfig.IsPotFallbackGatingEnabled((uint)cycle.PredictedNextPotFateId);
            PotFallbackStartDecision decision = PotFallbackWindow.Evaluate(
                cycle,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(Math.Max(0, fatesConfig.FateFallbackCutoffMinutes)),
                fatesConfig.PotSpawnLeadMinutes,
                potFarming,
                "FATE");
            if (!decision.AllowStart)
            {
                return false;
            }
        }

        if (fatesConfig.MinPotFateMinutesRemaining <= 0 || !isPot)
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
