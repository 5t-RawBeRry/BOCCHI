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
        if ((!context.IsIllegalMode && !context.IsCompletionist) || context.IsPotsAndTreasure)
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
            ResetSession();
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

        if (survey.PendingMapHunt)
        {
            TryStartPendingMapHunt(survey);
            return;
        }

        if (ShouldStartHunt(survey))
        {
            EnterHuntPhase(fromSurvey: true);
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

        // TriageLatchService owns raise latch; wait until it finishes before Sight / map hunt.
        if (TriageSession.IsActive(memory))
        {
            return;
        }

        LatchPostActivityHunt(survey, "activity completed");
    }

    private void LatchPostActivityHunt(AutomaticTreasureSurveyMemory survey, string reason)
    {
        if (!SupportJobTreasureSight.CanCast(supportJobs))
        {
            survey.PendingSurvey = false;
            survey.WaitingForSurveyResult = false;
            survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
            survey.PendingMapHunt = true;
            LogSightUnavailableOnce();
            logger.Info("Illegal Mode: latched map treasure hunt without Treasure Sight ({Reason})", reason);
            return;
        }

        survey.PendingMapHunt = false;
        survey.PendingSurvey = true;
        survey.WaitingForSurveyResult = false;
        survey.MinAcceptedRevision = tracker.SurveyRevision;
        survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
        logger.Info("Illegal Mode: latched Treasure Sight survey ({Reason})", reason);
    }

    private void ClearSurveyLatchIfSightUnavailable(AutomaticTreasureSurveyMemory survey)
    {
        if (SupportJobTreasureSight.CanCast(supportJobs))
        {
            loggedSightUnavailable = false;
            return;
        }

        if (survey.PendingSurvey || survey.WaitingForSurveyResult)
        {
            survey.PendingSurvey = false;
            survey.WaitingForSurveyResult = false;
            survey.SurveyWaitDeadlineUtc = DateTime.MinValue;
            survey.PendingMapHunt = true;
            LogSightUnavailableOnce();
            logger.Info("Illegal Mode: Treasure Sight became unavailable — falling back to map hunt");
        }
    }

    private void LogSightUnavailableOnce()
    {
        if (loggedSightUnavailable)
        {
            return;
        }

        loggedSightUnavailable = true;
        logger.Info(
            "Illegal Mode: Treasure Sight unavailable (Freelancer below level {Level}) — using built-in coffer map",
            SupportJobTreasureSight.RequiredFreelancerLevel);
    }

    private void TryStartPendingMapHunt(AutomaticTreasureSurveyMemory survey)
    {
        if (!hunter.IsVnavAvailable || TriageSession.IsActive(memory))
        {
            return;
        }

        if (automator.CurrentState is not (AutomatorState.Idle or null))
        {
            return;
        }

        survey.PendingMapHunt = false;
        EnterHuntPhase(fromSurvey: false);
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
        EnterHuntPhase(fromSurvey: true);
    }

    private void OnFillerHuntEnded(AutomaticTreasureSurveyMemory survey)
    {
        // After a route, wait for the next activity before surveying / hunting again.
        survey.PendingSurvey = false;
        survey.WaitingForSurveyResult = false;
        survey.PendingMapHunt = false;
        survey.MinAcceptedRevision = tracker.SurveyRevision;
        automator.SetSuspendedForTreasure(false);
        logger.Info("Illegal Mode: treasure hunt ended — will fill again after next activity");
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

    private void EnterHuntPhase(bool fromSurvey)
    {
        automator.SetSuspendedForTreasure(true);

        if (!hunter.IsVnavReady)
        {
            if (!fromSurvey)
            {
                // Keep retrying the map hunt once navmesh is ready.
                if (memory.TryRemember(out AutomaticTreasureSurveyMemory survey))
                {
                    survey.PendingMapHunt = true;
                }
            }

            return;
        }

        if (!hunter.Running)
        {
            hunter.ManagedByIllegalModeFiller = true;
            hunter.StartManaged();
            hadFillerHunt = true;
            if (fromSurvey && tracker.CountInitialised)
            {
                logger.Info(
                    "Illegal Mode: started automatic treasure hunt (survey {Silver} silver, {Bronze} bronze)",
                    tracker.SilverChests,
                    tracker.BronzeChests);
            }
            else
            {
                logger.Info("Illegal Mode: started automatic treasure hunt from built-in map (no Treasure Sight)");
            }

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
