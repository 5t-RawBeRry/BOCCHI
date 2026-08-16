using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Automator.Services.Goals;
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
    IFateRepository fateRepository,
    IGoalFactory goalFactory,
    IBuffProvider buffs,
    BuffConfig buffConfig,
    AutomatorConfig automatorConfig,
    FatesConfig fatesConfig,
    PotsConfig potsConfig,
    IFateScorer fateScorer,
    IPotCycleTracker potCycle,
    IZoneProvider zones,
    IFieldNoteTracker fieldNotes,
    IStartableCriticalEncounterFinder startableCriticalEncounters,
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

        if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
            || memory.TryRemember<PendingPotChestFarmMemory>(out PendingPotChestFarmMemory _))
        {
            return StatePriority.Never;
        }

        if (TriageSession.IsActive(memory))
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<AutomaticTreasureSurveyMemory>(out AutomaticTreasureSurveyMemory survey)
            && survey.IsBusy)
        {
            return StatePriority.Never;
        }

        // Only claim Choosing when something can actually start (avoids pot-cutoff softlock).
        bool hasCriticalEncounter = !PotsOnly && startableCriticalEncounters.FindStartable() != null;
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
            CriticalEncounter? criticalEncounter = startableCriticalEncounters.FindStartable();
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

        TryChoosePotPreposition();
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
                automatorConfig.ShouldFarmPotChests,
                automatorConfig.ShouldPrepositionToPots);
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
            else if (!fatesConfig.IsFateEnabledForIllegalMode(
                         fate.Id.Value,
                         isPot,
                         automatorConfig.PreferPotFates,
                         automatorConfig.ShouldFarmPotChests))
            {
                continue;
            }

            if (CompletionistBlocksFate(fate.Id.Value))
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

    /// <summary>Last reason prepositioning was skipped — logged only when it changes (called per tick).</summary>
    private string? lastPrepositionSkip;

    private bool CanPrepositionToPot(out FateId potId)
    {
        potId = default;

        if (!PotsOnly && !automatorConfig.ShouldPrepositionToPots)
        {
            return SkipPreposition("\"Wait near pots before they spawn\" is off");
        }

        PotCycleSnapshot cycle = potCycle.Snapshot;
        if (!cycle.HasPredictedNextPot)
        {
            return SkipPreposition("no pot spawn predicted yet");
        }

        if (!PotsOnly && !fatesConfig.IsPotFallbackGatingEnabled(
                (uint)cycle.PredictedNextPotFateId,
                automatorConfig.ShouldDoFates,
                automatorConfig.PreferPotFates,
                automatorConfig.ShouldFarmPotChests,
                automatorConfig.ShouldPrepositionToPots))
        {
            return SkipPreposition(
                !automatorConfig.ShouldDoFates
                    ? "\"Do FATEs\" is off"
                    : $"pot FATE {cycle.PredictedNextPotFateId} is not an allowed FATE "
                      + "(tick it under Allowed FATEs, or turn on \"Prefer pot FATEs\")");
        }

        if (CompletionistBlocksFate((uint)cycle.PredictedNextPotFateId))
        {
            return SkipPreposition($"Completionist has nothing left to log on pot FATE {cycle.PredictedNextPotFateId}");
        }

        FateId predicted = new((ushort)cycle.PredictedNextPotFateId);
        if (fateRepository.HasFate(predicted))
        {
            return SkipPreposition("pot FATE is already live — going to it, not prepositioning");
        }

        PotFallbackStartDecision decision = PotFallbackWindow.Evaluate(
            cycle,
            DateTimeOffset.UtcNow,
            GetPotPrepositionCutoff(),
            GetPotPrepositionLead(),
            true,
            "preposition");

        // ShouldPreposition is !AllowStart — the same window that blocks starting a FATE/CE.
        if (decision.AllowStart)
        {
            return SkipPreposition(decision.Reason);
        }

        lastPrepositionSkip = null;
        potId = predicted;
        return true;
    }

    /// <summary>Always false; records why so the reason can be logged once per change.</summary>
    private bool SkipPreposition(string reason)
    {
        if (lastPrepositionSkip != reason)
        {
            lastPrepositionSkip = reason;
            logger.Info("Not prepositioning to pot: {Reason}", reason);
        }

        return false;
    }

    private (TimeSpan Cutoff, int Lead) GetIllegalPotWindow() =>
    (
        TimeSpan.FromMinutes(Math.Max(0, potsConfig.FateFallbackCutoffMinutes)),
        potsConfig.PotSpawnLeadMinutes
    );

    private TimeSpan GetPotPrepositionCutoff() =>
        PotsOnly ? TimeSpan.Zero : TimeSpan.FromMinutes(Math.Max(0, potsConfig.FateFallbackCutoffMinutes));

    private int GetPotPrepositionLead() => potsConfig.PotSpawnLeadMinutes;

    private bool CompletionistBlocksFate(uint fateId) =>
        !PotsOnly
        && automatorContext.IsCompletionist
        && !fieldNotes.ShouldPursueFate(fateId);
}
