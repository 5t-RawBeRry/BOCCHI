using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Ocelot.Services.Logger;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ChoosingActivityHandler
(
    IAutomatorMemory memory,
    IAutomatorContext automatorContext,
    ICriticalEncounterRepository criticalEncounterRepository,
    IFateRepository fateRepository,
    IGoalFactory goalFactory,
    IBuffProvider buffs,
    BuffConfig buffConfig,
    AutomatorConfig automatorConfig,
    FatesConfig fatesConfig,
    PotsConfig potsConfig,
    CriticalEncountersConfig criticalEncountersConfig,
    IFateScorer fateScorer,
    IPotCycleTracker potCycle,
    IZoneProvider zones,
    ILogger<ChoosingActivityHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.ChoosingActivity)
{
    private bool PotsOnly => automatorContext.IsPotsAndTreasure;

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<GoalMemory>(out GoalMemory _))
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<NavigationInterruptedMemory>(out NavigationInterruptedMemory _))
        {
            return StatePriority.Never;
        }

        if (buffConfig.ShouldAutomateBuffs && buffs.ShouldRefreshAny())
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _))
        {
            return StatePriority.Never;
        }

        // Only claim Choosing when Handle can actually start something (avoids pot-cutoff softlock).
        bool hasCriticalEncounter = !PotsOnly && FindStartableCriticalEncounter() != null;
        if (!hasCriticalEncounter
            && FindStartableFate() == null
            && !CanPrepositionToPot(out _))
        {
            return StatePriority.Never;
        }

        return StatePriority.Low;
    }

    public override void Handle()
    {
        if (!PotsOnly)
        {
            CriticalEncounter? criticalEncounter = FindStartableCriticalEncounter();
            if (criticalEncounter != null)
            {
                IGoal goal = goalFactory.CriticalEncounter(criticalEncounter.Id);
                memory.TryAdd(new GoalMemory(goal));
                logger.Info("Chose CE {Id} ({Name})", criticalEncounter.Id.Value, criticalEncounter.Name);
                return;
            }
        }

        Fate? fate = FindStartableFate();
        if (fate != null)
        {
            IGoal goal = goalFactory.Fate(fate.Id);
            memory.TryAdd(new GoalMemory(goal));
            logger.Info("Chose FATE {Id} ({Name})", fate.Id.Value, fate.Name);
            return;
        }

        if (TryChoosePotPreposition())
        {
            return;
        }

        logger.Debug("ChoosingActivity: no eligible goal this tick");
    }

    private Fate? FindStartableFate()
    {
        IReadOnlyList<Fate> snapshot = fateRepository.Snapshot();
        if (snapshot.Count == 0)
        {
            return null;
        }

        if (!PotsOnly && !automatorConfig.ShouldDoFates)
        {
            return null;
        }

        Fate? best = null;
        float bestScore = float.MinValue;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PotCycleSnapshot cycle = potCycle.Snapshot;
        bool potFarming = PotsOnly
            || fatesConfig.IsPotFallbackGatingEnabled(
                (uint)cycle.PredictedNextPotFateId,
                automatorConfig.ShouldDoFates,
                automatorConfig.PreferPotFates,
                automatorConfig.ShouldFarmPotChests);
        IZone zone = zones.GetZone();

        foreach (Fate fate in snapshot)
        {
            bool isPot = zone.IsPotFate(fate.Id.Value);
            if (PotsOnly)
            {
                if (!isPot)
                {
                    continue;
                }
            }
            else if (!fatesConfig.IsFateEnabled(fate.Id.Value))
            {
                continue;
            }

            if (!isPot)
            {
                (TimeSpan cutoff, int lead) = GetIllegalPotWindow();
                PotFallbackStartDecision decision = PotFallbackWindow.Evaluate(
                    cycle,
                    now,
                    cutoff,
                    lead,
                    potFarming,
                    "FATE");
                if (!decision.AllowStart)
                {
                    continue;
                }
            }

            // Pot-only mode: accept pots even when they sit in DisabledFateIds.
            float scoreValue = PotsOnly && isPot
                ? Math.Max(1f, fateScorer.Score(fate).Value)
                : fateScorer.Score(fate).Value;
            if (scoreValue <= 0f || scoreValue <= bestScore)
            {
                continue;
            }

            bestScore = scoreValue;
            best = fate;
        }

        return best;
    }

    private bool TryChoosePotPreposition()
    {
        if (!CanPrepositionToPot(out FateId potId))
        {
            return false;
        }

        IGoal goal = goalFactory.Fate(potId);
        memory.TryAdd(new GoalMemory(goal));
        logger.Info("Prepositioning to pot FATE {Id} before spawn", potId.Value);
        return true;
    }

    private bool CanPrepositionToPot(out FateId potId)
    {
        potId = default;
        if (!PotsOnly && !automatorConfig.ShouldPrepositionToPots)
        {
            return false;
        }

        PotCycleSnapshot cycle = potCycle.Snapshot;
        if (!cycle.HasPredictedNextPot)
        {
            return false;
        }

        if (!PotsOnly && !fatesConfig.IsPotFallbackGatingEnabled(
                (uint)cycle.PredictedNextPotFateId,
                automatorConfig.ShouldDoFates,
                automatorConfig.PreferPotFates,
                automatorConfig.ShouldFarmPotChests))
        {
            return false;
        }

        FateId predicted = new((ushort)cycle.PredictedNextPotFateId);
        if (fateRepository.HasFate(predicted))
        {
            return false;
        }

        if (!PotFallbackWindow.ShouldPreposition(
                cycle,
                DateTimeOffset.UtcNow,
                GetPotPrepositionCutoff(),
                GetPotPrepositionLead(),
                true))
        {
            return false;
        }

        potId = predicted;
        return true;
    }

    private (TimeSpan Cutoff, int Lead) GetIllegalPotWindow() =>
    (
        TimeSpan.FromMinutes(Math.Max(0, potsConfig.FateFallbackCutoffMinutes)),
        potsConfig.PotSpawnLeadMinutes
    );

    private TimeSpan GetPotPrepositionCutoff() =>
        PotsOnly ? TimeSpan.Zero : TimeSpan.FromMinutes(Math.Max(0, potsConfig.FateFallbackCutoffMinutes));

    private int GetPotPrepositionLead() =>
        PotsOnly ? PotsTreasureDefaults.PrepositionLeadMinutes : potsConfig.PotSpawnLeadMinutes;

    private CriticalEncounter? FindStartableCriticalEncounter()
    {
        if (PotsOnly || !automatorConfig.ShouldDoCriticalEncounters)
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

        // Register + Warmup — Warmup-only used to leave Choosing stuck with a visible CE.
        foreach (CriticalEncounter ce in criticalEncounterRepository.SnapshotWithoutForkedTower())
        {
            if (!ce.IsPreparing() || !criticalEncountersConfig.IsCriticalEncounterEnabled(ce.Id.Value))
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
                logger.Debug(decision.Reason);
                continue;
            }

            return ce;
        }

        return null;
    }
}
