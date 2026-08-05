using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;

namespace BOCCHI.Automator.Services;

/// <summary>
///     AOCC-style Illegal Mode treasure filler: after CE/FATE, Return to camp, cast Treasure Sight,
///     then start a hunt when survey thresholds pass (with rescan deferral when they don't).
/// </summary>
public class IllegalModeTreasureFillerService
(
    IAutomator automator,
    IAutomatorContext context,
    IAutomatorMemory memory,
    ITreasureHunter hunter,
    ITreasureTracker tracker,
    ISupportJobFactory supportJobs,
    IZoneProvider zones,
    TreasureConfig treasureConfig,
    ILogger<IllegalModeTreasureFillerService> logger
) : IOnUpdate
{
    private bool hadActivity;

    private bool hadFillerHunt;

    private bool loggedSightUnavailable;

    public void Update()
    {
        if (!context.IsIllegalMode || context.IsPotsAndTreasure)
        {
            ResetSession();
            return;
        }

        if (!treasureConfig.EnableAutomaticTreasureHuntDuringIllegalMode)
        {
            ResetSession();
            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        EnsureSurveyMemory(out AutomaticTreasureSurveyMemory survey);
        ClearSurveyLatchIfSightUnavailable(survey);

        bool activityNow = HasActivityWork();
        if (hadActivity && !activityNow)
        {
            OnActivityCompleted(survey);
        }

        hadActivity = activityNow;

        if (hunter.ManagedByIllegalModeFiller && hunter.Running)
        {
            hadFillerHunt = true;
            return;
        }

        if (hadFillerHunt && (!hunter.Running || !hunter.ManagedByIllegalModeFiller))
        {
            OnFillerHuntEnded(survey);
            hadFillerHunt = false;
        }

        if (activityNow)
        {
            PauseFillerHuntForActivity();
            return;
        }

        if (survey.WaitingForSurveyResult)
        {
            TryApplySurveyResult(survey);
            return;
        }

        if (survey.PendingSurvey)
        {
            // CastingTreasureSightHandler casts at camp; ReturningHandler gets us there.
            return;
        }

        if (ShouldStartHunt(survey))
        {
            EnterHuntPhase();
        }
    }

    private void EnsureSurveyMemory(out AutomaticTreasureSurveyMemory survey)
    {
        if (memory.TryRemember(out survey))
        {
            return;
        }

        survey = new AutomaticTreasureSurveyMemory();
        memory.TryAdd(survey);
    }

    private bool HasActivityWork()
    {
        if (memory.TryRemember<GoalMemory>(out GoalMemory _))
        {
            return true;
        }

        if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _))
        {
            return true;
        }

        if (memory.TryRemember<WaitingForPotFateMemory>(out WaitingForPotFateMemory _))
        {
            return true;
        }

        if (memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory _))
        {
            return true;
        }

        if (memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
        {
            return true;
        }

        if (memory.TryRemember<CastingTreasureSightMemory>(out CastingTreasureSightMemory _))
        {
            return true;
        }

        if (memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _))
        {
            return true;
        }

        return false;
    }

    private void OnActivityCompleted(AutomaticTreasureSurveyMemory survey)
    {
        if (survey.WaitingForSurveyResult || survey.PendingSurvey)
        {
            return;
        }

        if (!CanCastTreasureSight())
        {
            LogSightUnavailableOnce();
            return;
        }

        if (survey.IsRescanDue)
        {
            LatchSurvey(survey, "activity completed");
            return;
        }

        survey.RemainingSilverCompletionsUntilRescan = Math.Max(0, survey.RemainingSilverCompletionsUntilRescan - 1);
        survey.RemainingBronzeCompletionsUntilRescan = Math.Max(0, survey.RemainingBronzeCompletionsUntilRescan - 1);
        logger.Info(
            "Illegal Mode: deferred treasure survey — {Silver} silver / {Bronze} bronze completions until rescan",
            survey.RemainingSilverCompletionsUntilRescan,
            survey.RemainingBronzeCompletionsUntilRescan);

        if (survey.IsRescanDue)
        {
            LatchSurvey(survey, "rescan deferral elapsed");
        }
    }

    private void LatchSurvey(AutomaticTreasureSurveyMemory survey, string reason)
    {
        if (!CanCastTreasureSight())
        {
            LogSightUnavailableOnce();
            return;
        }

        survey.PendingSurvey = true;
        survey.WaitingForSurveyResult = false;
        survey.MinAcceptedRevision = tracker.SurveyRevision;
        survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
        logger.Info("Illegal Mode: latched Treasure Sight survey ({Reason})", reason);
    }

    private void ClearSurveyLatchIfSightUnavailable(AutomaticTreasureSurveyMemory survey)
    {
        if (CanCastTreasureSight())
        {
            loggedSightUnavailable = false;
            return;
        }

        if (!survey.PendingSurvey && !survey.WaitingForSurveyResult)
        {
            return;
        }

        survey.PendingSurvey = false;
        survey.WaitingForSurveyResult = false;
        survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
        LogSightUnavailableOnce();
    }

    private bool CanCastTreasureSight()
    {
        SupportJob freelancer = supportJobs.Create(SupportJobId.PhantomFreelancer);
        return freelancer.Level >= 10;
    }

    private void LogSightUnavailableOnce()
    {
        if (loggedSightUnavailable)
        {
            return;
        }

        loggedSightUnavailable = true;
        logger.Info(
            "Illegal Mode: Treasure Sight unavailable (Freelancer below level 10) — skipping auto survey/hunt until unlocked");
    }

    private void TryApplySurveyResult(AutomaticTreasureSurveyMemory survey)
    {
        if (tracker.SurveyRevision > survey.MinAcceptedRevision && tracker.CountInitialised)
        {
            ApplySurveyResult(survey);
            return;
        }

        if (survey.SurveyWaitDeadlineUtc != DateTime.MinValue
            && DateTime.UtcNow >= survey.SurveyWaitDeadlineUtc)
        {
            survey.WaitingForSurveyResult = false;
            survey.PendingSurvey = false;
            logger.Info("Illegal Mode: Treasure Sight survey timed out — retry after next activity");
        }
    }

    private void ApplySurveyResult(AutomaticTreasureSurveyMemory survey)
    {
        survey.WaitingForSurveyResult = false;
        survey.PendingSurvey = false;

        int silver = tracker.SilverChests;
        int bronze = tracker.BronzeChests;
        bool met = TreasureSurveyGate.MeetsThresholds(
            silver,
            bronze,
            treasureConfig.AutomaticTreasureSilverThreshold,
            treasureConfig.AutomaticTreasureBronzeThreshold);

        if (met)
        {
            survey.RemainingSilverCompletionsUntilRescan = 0;
            survey.RemainingBronzeCompletionsUntilRescan = 0;
            logger.Info(
                "Illegal Mode: survey met thresholds ({Silver} silver, {Bronze} bronze) — starting hunt",
                silver,
                bronze);
            EnterHuntPhase();
            return;
        }

        // AOCC: defer rescans by the deficit so we don't spam Sight every activity.
        if (treasureConfig.AutomaticTreasureSilverThreshold <= 0
            && treasureConfig.AutomaticTreasureBronzeThreshold <= 0
            && silver + bronze > 0)
        {
            survey.RemainingSilverCompletionsUntilRescan = 0;
            survey.RemainingBronzeCompletionsUntilRescan = 0;
        }
        else
        {
            survey.RemainingSilverCompletionsUntilRescan = TreasureSurveyGate.Deficit(
                treasureConfig.AutomaticTreasureSilverThreshold,
                silver);
            survey.RemainingBronzeCompletionsUntilRescan = TreasureSurveyGate.Deficit(
                treasureConfig.AutomaticTreasureBronzeThreshold,
                bronze);
        }

        logger.Info(
            "Illegal Mode: survey below thresholds ({Silver} silver, {Bronze} bronze) — defer {SilverLeft}/{BronzeLeft} completions",
            silver,
            bronze,
            survey.RemainingSilverCompletionsUntilRescan,
            survey.RemainingBronzeCompletionsUntilRescan);
    }

    private void OnFillerHuntEnded(AutomaticTreasureSurveyMemory survey)
    {
        // AOCC: after a route, require a fresh Sight on the next base-camp recovery.
        survey.PendingSurvey = false;
        survey.WaitingForSurveyResult = false;
        survey.MinAcceptedRevision = tracker.SurveyRevision;
        survey.RemainingSilverCompletionsUntilRescan = 0;
        survey.RemainingBronzeCompletionsUntilRescan = 0;
        automator.SetSuspendedForTreasure(false);
        logger.Info("Illegal Mode: treasure hunt ended — fresh survey required next idle");
    }

    private bool ShouldStartHunt(AutomaticTreasureSurveyMemory survey)
    {
        if (!hunter.IsVnavAvailable || survey.PendingSurvey || survey.WaitingForSurveyResult)
        {
            return false;
        }

        if (!tracker.CountInitialised || tracker.SurveyRevision <= survey.MinAcceptedRevision)
        {
            return false;
        }

        if (!TreasureSurveyGate.MeetsThresholds(
                tracker.SilverChests,
                tracker.BronzeChests,
                treasureConfig.AutomaticTreasureSilverThreshold,
                treasureConfig.AutomaticTreasureBronzeThreshold))
        {
            return false;
        }

        return automator.CurrentState is AutomatorState.Idle or null;
    }

    private void EnterHuntPhase()
    {
        automator.SetSuspendedForTreasure(true);

        if (!hunter.IsVnavReady)
        {
            return;
        }

        if (!hunter.Running)
        {
            hunter.ManagedByIllegalModeFiller = true;
            hunter.Toggle();
            hadFillerHunt = true;
            logger.Info(
                "Illegal Mode: started automatic treasure hunt (survey {Silver} silver, {Bronze} bronze)",
                tracker.SilverChests,
                tracker.BronzeChests);
            return;
        }

        if (hunter.Paused)
        {
            hunter.Resume();
            hadFillerHunt = true;
            logger.Info("Illegal Mode: resumed automatic treasure hunt");
        }
    }

    private void PauseFillerHuntForActivity()
    {
        automator.SetSuspendedForTreasure(false);

        if (!hunter.ManagedByIllegalModeFiller)
        {
            return;
        }

        if (hunter.Running && !hunter.Paused)
        {
            hunter.Pause();
            logger.Info("Illegal Mode: paused treasure hunt for CE/FATE activity");
        }
    }

    private void ResetSession()
    {
        hadActivity = false;
        hadFillerHunt = false;
        loggedSightUnavailable = false;
        memory.Forget<AutomaticTreasureSurveyMemory>();

        if (hunter.ManagedByIllegalModeFiller)
        {
            automator.SetSuspendedForTreasure(false);
            hunter.ManagedByIllegalModeFiller = false;
            if (hunter.Running)
            {
                hunter.Toggle();
            }
        }
    }
}
