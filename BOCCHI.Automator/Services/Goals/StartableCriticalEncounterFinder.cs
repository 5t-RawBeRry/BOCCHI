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

        // A pot that is actually up outranks a CE. The window check below only looks at the *next*
        // predicted pot, which rolls a full cycle forward the moment one spawns — so it never covers
        // the live pot, and Choosing would send us to a warming-up CE mid-pot.
        if (cycle.CurrentActivePotFateId != 0
            && automatorConfig.ShouldDoFates
            && fatesConfig.IsFateEnabledForIllegalMode(
                (uint)cycle.CurrentActivePotFateId,
                isPotFate: true,
                automatorConfig.PreferPotFates,
                automatorConfig.ShouldFarmPotChests))
        {
            return null;
        }

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
