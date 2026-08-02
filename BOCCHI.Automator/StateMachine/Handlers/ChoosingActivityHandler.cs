using BOCCHI.Automator.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
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

        int enabledFates = fateRepository.Snapshot().Count(f => fatesConfig.IsFateEnabled(f.Id.Value));
        int criticalEncounters = criticalEncounterRepository.SnapshotWithoutForkedTower()
            .Count(ce => ce.State == DynamicEventState.Register
                         && criticalEncountersConfig.IsCriticalEncounterEnabled(ce.Id.Value));

        if (enabledFates <= 0 && criticalEncounters <= 0)
        {
            return StatePriority.Never;
        }

        return StatePriority.VeryLow;
    }

    public override void Handle()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        PotCycleSnapshot cycle = potCycle.Snapshot;
        bool potFarming = fatesConfig.IsPotFallbackGatingEnabled((uint)cycle.PredictedNextPotFateId);

        CriticalEncounter? criticalEncounter = criticalEncounterRepository.SnapshotWithoutForkedTower()
            .FirstOrDefault(c => c.State == DynamicEventState.Register
                                 && criticalEncountersConfig.IsCriticalEncounterEnabled(c.Id.Value));
        if (criticalEncounter != null)
        {
            PotFallbackStartDecision ceDecision = PotFallbackWindow.Evaluate(
                cycle,
                now,
                TimeSpan.FromMinutes(Math.Max(0, fatesConfig.CeFallbackCutoffMinutes)),
                fatesConfig.PotSpawnLeadMinutes,
                potFarming,
                "CE");
            if (!ceDecision.AllowStart)
            {
                logger.Debug(ceDecision.Reason);
            }
            else
            {
                IGoal goal = goalFactory.CriticalEncounter(criticalEncounter.Id);
                memory.TryAdd(new GoalMemory(goal));
                return;
            }
        }

        Fate? fate = fateScorer.SelectBest(fateRepository.Snapshot());
        if (fate != null)
        {
            // Always allow pot FATEs themselves; only gate non-pot FATE fallbacks.
            if (!zones.GetZone().IsPotFate(fate.Id.Value))
            {
                PotFallbackStartDecision fateDecision = PotFallbackWindow.Evaluate(
                    cycle,
                    now,
                    TimeSpan.FromMinutes(Math.Max(0, fatesConfig.FateFallbackCutoffMinutes)),
                    fatesConfig.PotSpawnLeadMinutes,
                    potFarming,
                    "FATE");
                if (!fateDecision.AllowStart)
                {
                    logger.Debug(fateDecision.Reason);
                    return;
                }
            }

            IGoal goal = goalFactory.Fate(fate.Id);
            memory.TryAdd(new GoalMemory(goal));
            return;
        }

        // Pot cutoff or empty board — wait for next tick rather than hard-failing.
        logger.Debug("ChoosingActivity: no eligible goal this tick");
    }
}
