using BOCCHI.Automator.Data;
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
    ICriticalEncounterRepository criticalEncounterRepository,
    IFateRepository fateRepository,
    IGoalFactory goalFactory,
    IBuffProvider buffs,
    BuffConfig buffConfig,
    FatesConfig fatesConfig,
    CriticalEncountersConfig criticalEncountersConfig,
    IFateScorer fateScorer,
    IPotCycleTracker potCycle,
    IZoneProvider zones,
    ILogger<ChoosingActivityHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.ChoosingActivity)
{
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
        if (FindStartableCriticalEncounter() == null
            && FindStartableFate() == null
            && !CanPrepositionToPot(out _))
        {
            return StatePriority.Never;
        }

        return StatePriority.Low;
    }

    public override void Handle()
    {
        CriticalEncounter? criticalEncounter = FindStartableCriticalEncounter();
        if (criticalEncounter != null)
        {
            IGoal goal = goalFactory.CriticalEncounter(criticalEncounter.Id);
            memory.TryAdd(new GoalMemory(goal));
            logger.Info("Chose CE {Id} ({Name})", criticalEncounter.Id.Value, criticalEncounter.Name);
            return;
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
        // Prefer highest score among FATEs that pot cutoff will actually allow.
        IReadOnlyList<Fate> snapshot = fateRepository.Snapshot();
        if (!fatesConfig.ShouldDoFates || snapshot.Count == 0)
        {
            return null;
        }

        Fate? best = null;
        float bestScore = float.MinValue;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PotCycleSnapshot cycle = potCycle.Snapshot;
        bool potFarming = fatesConfig.IsPotFallbackGatingEnabled((uint)cycle.PredictedNextPotFateId);

        foreach (Fate fate in snapshot)
        {
            if (!fatesConfig.IsFateEnabled(fate.Id.Value))
            {
                continue;
            }

            if (!zones.GetZone().IsPotFate(fate.Id.Value))
            {
                PotFallbackStartDecision decision = PotFallbackWindow.Evaluate(
                    cycle,
                    now,
                    TimeSpan.FromMinutes(Math.Max(0, fatesConfig.FateFallbackCutoffMinutes)),
                    fatesConfig.PotSpawnLeadMinutes,
                    potFarming,
                    "FATE");
                if (!decision.AllowStart)
                {
                    continue;
                }
            }

            FateScore score = fateScorer.Score(fate);
            if (score.Value <= 0f || score.Value <= bestScore)
            {
                continue;
            }

            bestScore = score.Value;
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
        if (!fatesConfig.ShouldPrepositionToPots)
        {
            return false;
        }

        PotCycleSnapshot cycle = potCycle.Snapshot;
        if (!fatesConfig.IsPotFallbackGatingEnabled((uint)cycle.PredictedNextPotFateId)
            || !cycle.HasPredictedNextPot)
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
                TimeSpan.FromMinutes(Math.Max(0, fatesConfig.FateFallbackCutoffMinutes)),
                fatesConfig.PotSpawnLeadMinutes,
                true))
        {
            return false;
        }

        potId = predicted;
        return true;
    }

    private CriticalEncounter? FindStartableCriticalEncounter()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PotCycleSnapshot cycle = potCycle.Snapshot;
        bool potFarming = fatesConfig.IsPotFallbackGatingEnabled((uint)cycle.PredictedNextPotFateId);

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
                TimeSpan.FromMinutes(Math.Max(0, fatesConfig.CeFallbackCutoffMinutes)),
                fatesConfig.PotSpawnLeadMinutes,
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
