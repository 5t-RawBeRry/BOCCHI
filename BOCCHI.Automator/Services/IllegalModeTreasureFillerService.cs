using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;

namespace BOCCHI.Automator.Services;

/// <summary>Illegal Mode post-activity treasure filler.</summary>
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
    // Default Order (0). TriageLatchService is Order 10 so PendingTriage is set before Sight latches.
    public int Order => 0;

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

        bool activityNow = IllegalModeActivityWork.HasFillerBlockingActivity(memory);
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

    private void OnActivityCompleted(AutomaticTreasureSurveyMemory survey)
    {
        if (survey.IsBusy)
        {
            return;
        }

        // TriageLatchService owns raise latch; wait until it finishes before Sight.
        if (TriageSession.IsActive(memory))
        {
            return;
        }

        LatchSurvey(survey, "activity completed");
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

        if (!survey.IsBusy)
        {
            return;
        }

        survey.PendingSurvey = false;
        survey.WaitingForSurveyResult = false;
        survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
        LogSightUnavailableOnce();
    }

    private bool CanCastTreasureSight() => SupportJobTreasureSight.CanCast(supportJobs);

    private void LogSightUnavailableOnce()
    {
        if (loggedSightUnavailable)
        {
            return;
        }

        loggedSightUnavailable = true;
        logger.Info(
            "Illegal Mode: Treasure Sight unavailable (Freelancer below level {Level}) — skipping auto survey/hunt until unlocked",
            SupportJobTreasureSight.RequiredFreelancerLevel);
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
        if (silver + bronze <= 0)
        {
            logger.Info("Illegal Mode: survey found no coffers — continuing CE/FATE farming");
            return;
        }

        logger.Info(
            "Illegal Mode: survey found {Silver} silver, {Bronze} bronze — starting hunt",
            silver,
            bronze);
        EnterHuntPhase();
    }

    private void OnFillerHuntEnded(AutomaticTreasureSurveyMemory survey)
    {
        // After a route, require a fresh Sight on the next idle.
        survey.PendingSurvey = false;
        survey.WaitingForSurveyResult = false;
        survey.MinAcceptedRevision = tracker.SurveyRevision;
        automator.SetSuspendedForTreasure(false);
        logger.Info("Illegal Mode: treasure hunt ended — fresh survey required next idle");
    }

    private bool ShouldStartHunt(AutomaticTreasureSurveyMemory survey)
    {
        if (!hunter.IsVnavAvailable || survey.IsBusy)
        {
            return false;
        }

        if (!tracker.CountInitialised || tracker.SurveyRevision <= survey.MinAcceptedRevision)
        {
            return false;
        }

        if (tracker.SilverChests + tracker.BronzeChests <= 0)
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
            hunter.StartManaged();
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
