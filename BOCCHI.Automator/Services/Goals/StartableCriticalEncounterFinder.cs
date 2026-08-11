using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Ocelot.Services.Logger;

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
    IFieldNoteTracker fieldNotes,
    ILogger<StartableCriticalEncounterFinder> logger
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
            automatorConfig.ShouldFarmPotChests);

        // Include Warmup — Warmup-only used to leave Choosing stuck with a visible CE.
        foreach (CriticalEncounter ce in criticalEncounterRepository.SnapshotWithoutForkedTower())
        {
            if (!ce.IsPreparing() || !criticalEncountersConfig.IsCriticalEncounterEnabled(ce.Id.Value))
            {
                continue;
            }

            if (automatorContext.IsCompletionist && !fieldNotes.ShouldPursueCriticalEncounter(ce.Id.Value))
            {
                logger.Debug("CE {Id} skipped — completionist already has field note", ce.Id.Value);
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
                logger.Debug(decision.Reason);
                continue;
            }

            return ce;
        }

        return null;
    }
}
