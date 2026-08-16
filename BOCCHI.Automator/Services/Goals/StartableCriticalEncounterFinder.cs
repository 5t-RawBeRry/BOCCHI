using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services.Goals;

public class StartableCriticalEncounterFinder
(
    IAutomatorContext automatorContext,
    AutomatorConfig automatorConfig,
    FatesConfig fatesConfig,
    PotsConfig potsConfig,
    CriticalEncountersConfig criticalEncountersConfig,
    ICriticalEncounterRepository criticalEncounterRepository,
    IPotCycleTracker potCycle,
    IFieldNoteTracker fieldNotes
) : IStartableCriticalEncounterFinder
{
    public CriticalEncounter? FindStartable()
    {
        if (automatorContext.IsPotsAndTreasure || !automatorConfig.ShouldDoCriticalEncounters)
        {
            return null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        PotCycleSnapshot cycle = potCycle.Snapshot;
        bool potFarming = fatesConfig.IsPotFallbackGatingEnabled(
            (uint)cycle.PredictedNextPotFateId,
            automatorConfig.ShouldDoFates,
            automatorConfig.PreferPotFates,
            automatorConfig.ShouldFarmPotChests,
            automatorConfig.ShouldPrepositionToPots);

        // Include Warmup so Choosing does not stall on a visible CE.
        foreach (CriticalEncounter ce in criticalEncounterRepository.SnapshotWithoutForkedTower())
        {
            if (!ce.IsPreparing() || !criticalEncountersConfig.IsCriticalEncounterEnabled(ce.Id.Value))
            {
                continue;
            }

            if (automatorContext.IsCompletionist && !fieldNotes.ShouldPursueCriticalEncounter(ce.Id.Value))
            {
                continue;
            }

            PotFallbackStartDecision decision = PotFallbackWindow.Evaluate(
                cycle,
                now,
                TimeSpan.FromMinutes(Math.Max(0, potsConfig.CeFallbackCutoffMinutes)),
                potsConfig.PotSpawnLeadMinutes,
                potFarming,
                "CE");
            if (!decision.AllowStart)
            {
                continue;
            }

            return ce;
        }

        return null;
    }
}
