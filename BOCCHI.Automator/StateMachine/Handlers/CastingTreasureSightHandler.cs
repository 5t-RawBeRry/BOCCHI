using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class CastingTreasureSightHandler
(
    ICondition conditions,
    IZoneProvider zone,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer,
    IAutomatorMemory memory,
    AutomatorConfig config,
    TreasureConfig treasureConfig,
    ITreasureTracker tracker
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.CastingTreasureSight)
{
    private DateTime lastCast = DateTime.MinValue;

    public override StatePriority GetScore()
    {
        // Sticky Critical so ReturningToJob cannot preempt mid-cast Freelancer swap.
        if (memory.TryRemember<CastingTreasureSightMemory>(out CastingTreasureSightMemory _))
        {
            return StatePriority.Critical;
        }

        if (memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
        {
            return StatePriority.Never;
        }

        if (!SupportJobTreasureSight.CanCast(supportJobs))
        {
            return StatePriority.Never;
        }

        if (!zone.GetZone().IsInBasecamp())
        {
            return StatePriority.Never;
        }

        // Post-activity survey latch owns Sight while auto-hunt is enabled.
        if (treasureConfig.EnableAutomaticTreasureHuntDuringIllegalMode
            && memory.TryRemember<AutomaticTreasureSurveyMemory>(out AutomaticTreasureSurveyMemory survey)
            && survey.PendingSurvey
            && !survey.WaitingForSurveyResult)
        {
            return StatePriority.Always;
        }

        // Periodic basecamp Sight only when auto-hunt is off (latch replaces it otherwise).
        if (!treasureConfig.EnableAutomaticTreasureHuntDuringIllegalMode
            && config.ShouldCastTreasureSight
            && GetLastCastDeltaSeconds() >= config.TreasureSightRecastIntervalSeconds)
        {
            return StatePriority.Always;
        }

        return StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();

        // Only remember a non-Freelancer job — re-entering while already Freelancer must not
        // overwrite a real previous job with Freelancer (TryAdd) or leave nothing to restore.
        if (supportJobs.TryGetCurrent(out SupportJob current)
            && current.Id != SupportJobId.PhantomFreelancer)
        {
            memory.Forget<TreasureSightSupportJobMemory>();
            memory.TryAdd(new TreasureSightSupportJobMemory(current.Id));
        }

        memory.TryAdd<CastingTreasureSightMemory>();
    }

    public override void Handle()
    {
        if (!supportJobs.TryGetCurrent(out SupportJob current))
        {
            return;
        }

        if (DismountAssist.TryDismount(conditions))
        {
            return;
        }

        if (current.Id != SupportJobId.PhantomFreelancer)
        {
            if (!changer.IsBusy() && !PhantomJobChangeGate.IsBlocked(conditions))
            {
                changer.Change(SupportJobId.PhantomFreelancer);
            }

            return;
        }

        if (Actions.PhantomActionII.CanCast())
        {
            if (Actions.PhantomActionII.Cast())
            {
                lastCast = DateTime.Now;
                memory.Forget<CastingTreasureSightMemory>();

                if (memory.TryRemember<AutomaticTreasureSurveyMemory>(out AutomaticTreasureSurveyMemory survey)
                    && survey.PendingSurvey)
                {
                    survey.PendingSurvey = false;
                    survey.WaitingForSurveyResult = true;
                    survey.MinAcceptedRevision = tracker.SurveyRevision;
                    survey.SurveyWaitDeadlineUtc = DateTime.UtcNow + TimeSpan.FromSeconds(8);
                }

                // Job restore is ReturningToJobHandler (must beat Pathfinding priority).
            }
        }
    }

    private int GetLastCastDeltaSeconds()
    {
        if (lastCast == DateTime.MinValue)
        {
            return int.MaxValue;
        }

        return (int)(DateTime.Now - lastCast).TotalSeconds;
    }
}
