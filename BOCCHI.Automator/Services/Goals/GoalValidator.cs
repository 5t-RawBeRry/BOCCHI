using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Ocelot.Services.Logger;

namespace BOCCHI.Automator.Services.Goals;

public class GoalValidator
(
    ICriticalEncounterRepository criticalEncounterRepository,
    ICriticalEncounterContext criticalEncounterContext,
    IFateRepository fateRepository,
    IFateContext fateContext,
    IZoneProvider zones,
    AutomatorConfig automatorConfig,
    FatesConfig fatesConfig,
    PotsConfig potsConfig,
    CriticalEncountersConfig criticalEncountersConfig,
    IPotCycleTracker potCycle,
    IAutomatorContext automatorContext,
    IAutomatorMemory memory,
    IFieldNoteTracker fieldNotes,
    IStartableCriticalEncounterFinder startableCriticalEncounters,
    ILogger<GoalValidator> logger
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
        if (automatorContext.IsPotsAndTreasure)
        {
            return false;
        }

        if (!automatorConfig.ShouldDoCriticalEncounters
            || !criticalEncountersConfig.IsCriticalEncounterEnabled(id.Value))
        {
            return false;
        }

        CriticalEncounter? ce = criticalEncounterRepository.SnapshotWithoutForkedTower()
            .FirstOrDefault(c => c.Id == id);
        if (ce == null)
        {
            return false;
        }

        if (ce.IsPreparing())
        {
            return PassesCompletionistCriticalEncounter(id.Value);
        }

        if (!ce.IsActive())
        {
            return false;
        }

        // Battle: keep goal when EventId matches, we waited here, travel is suspended, or still pathing in.
        if (criticalEncounterContext.GetCriticalEncounterId() == id)
        {
            return true;
        }

        if (memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory wait)
            && wait.IsFor(id))
        {
            wait.MarkBattleStarted();
            return true;
        }

        if (memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _))
        {
            return true;
        }

        if (memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _))
        {
            return true;
        }

        return PassesCompletionistCriticalEncounter(id.Value);
    }

    private bool ValidateFate(FateId id)
    {
        bool isPot = zones.GetZone().IsPotFate(id.Value);
        bool potsOnly = automatorContext.IsPotsAndTreasure;

        if (potsOnly)
        {
            if (!isPot)
            {
                return false;
            }
        }
        else if (!automatorConfig.ShouldDoFates
                 || !fatesConfig.IsFateEnabledForIllegalMode(
                     id.Value,
                     isPot,
                     automatorConfig.PreferPotFates,
                     automatorConfig.ShouldFarmPotChests))
        {
            return false;
        }

        if (isPot && IsValidPotPreposition(id))
        {
            return PassesCompletionistFate(id.Value, potsOnly);
        }

        if (!fateRepository.HasFate(id))
        {
            return false;
        }

        // Live pot — stay until despawn (chest farm starts then). Keep pot goals over CEs.
        if (isPot)
        {
            return true;
        }

        // Yield to a CE only while still traveling; finish the FATE once registered in it.
        if (!potsOnly
            && fateContext.GetFateId() != id
            && startableCriticalEncounters.FindStartable() is { } ce)
        {
            logger.Info(
                "Invalidating FATE {FateId} (still pathing) — startable CE {CeId} ({CeName})",
                id.Value,
                ce.Id.Value,
                ce.Name);
            return false;
        }

        if (!PassesCompletionistFate(id.Value, potsOnly))
        {
            return false;
        }

        PotCycleSnapshot cycle = potCycle.Snapshot;
        bool potFarming = fatesConfig.IsPotFallbackGatingEnabled(
            (uint)cycle.PredictedNextPotFateId,
            automatorConfig.ShouldDoFates,
            automatorConfig.PreferPotFates,
            automatorConfig.ShouldFarmPotChests);
        (TimeSpan cutoff, int lead) = GetIllegalPotWindow();
        PotFallbackStartDecision decision = PotFallbackWindow.Evaluate(
            cycle,
            DateTimeOffset.UtcNow,
            cutoff,
            lead,
            potFarming,
            "FATE");
        return decision.AllowStart;
    }

    private bool PassesCompletionistFate(uint fateId, bool potsOnly) =>
        potsOnly
        || !automatorContext.IsCompletionist
        || fieldNotes.ShouldPursueFate(fateId);

    private bool PassesCompletionistCriticalEncounter(uint encounterId) =>
        !automatorContext.IsCompletionist
        || fieldNotes.ShouldPursueCriticalEncounter(encounterId);

    /// <summary>
    ///     Predicted pot goal kept before the FATE exists (and briefly after predicted spawn).
    /// </summary>
    private bool IsValidPotPreposition(FateId id)
    {
        bool potsOnly = automatorContext.IsPotsAndTreasure;
        if (!potsOnly && !automatorConfig.ShouldPrepositionToPots)
        {
            return false;
        }

        PotCycleSnapshot cycle = potCycle.Snapshot;
        if (cycle.PredictedNextPotFateId != id.Value)
        {
            return false;
        }

        if (!potsOnly && !fatesConfig.IsPotFallbackGatingEnabled(
                (uint)cycle.PredictedNextPotFateId,
                automatorConfig.ShouldDoFates,
                automatorConfig.PreferPotFates,
                automatorConfig.ShouldFarmPotChests))
        {
            return false;
        }

        // Drop if prediction is stale (spawn never observed).
        if (DateTimeOffset.UtcNow > cycle.PredictedNextSpawnAt + TimeSpan.FromMinutes(5))
        {
            return false;
        }

        // Once the FATE is up, normal HasFate validation takes over.
        if (fateRepository.HasFate(id))
        {
            return false;
        }

        return PotFallbackWindow.ShouldPreposition(
            cycle,
            DateTimeOffset.UtcNow,
            potsOnly ? TimeSpan.Zero : TimeSpan.FromMinutes(Math.Max(0, potsConfig.FateFallbackCutoffMinutes)),
            potsOnly ? PotsTreasureDefaults.PrepositionLeadMinutes : potsConfig.PotSpawnLeadMinutes,
            true);
    }

    private (TimeSpan Cutoff, int Lead) GetIllegalPotWindow() =>
    (
        TimeSpan.FromMinutes(Math.Max(0, potsConfig.FateFallbackCutoffMinutes)),
        potsConfig.PotSpawnLeadMinutes
    );
}
