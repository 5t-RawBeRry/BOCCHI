using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.States;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ApplyingBuffsHandler
(
    Func<IStateMachine<BuffState>> factory,
    IBuffProvider buffs,
    IZoneProvider zones,
    IAutomatorMemory memory,
    ISupportJobFactory jobs,
    IPathfinder pathfinder,
    BuffConfig config,
    ILogger<ApplyingBuffsHandler> logger
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.ApplyingBuffs)
{
    private IStateMachine<BuffState>? stateMachine;

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
        {
            return StatePriority.VeryHigh;
        }

        if (IllegalModeActivityWork.HasPendingJobRestore(memory)
            || memory.TryRemember<AutomaticTreasureSurveyMemory>(out AutomaticTreasureSurveyMemory survey)
               && survey.IsBusy)
        {
            return StatePriority.Never;
        }

        if (!config.ShouldAutomateBuffs || !buffs.ShouldRefreshAny())
        {
            return StatePriority.Never;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            return StatePriority.Never;
        }

        if (!zone.GetNearbyKnowledgeCrystals().Any())
        {
            return StatePriority.Never;
        }

        if (jobs.TryGetCurrent(out SupportJob current) && current.Id == SupportJobId.PhantomFreelancer)
        {
            return StatePriority.Never;
        }

        return StatePriority.MediumHigh;
    }

    public override void Enter()
    {
        stateMachine = factory();

        memory.TryAdd<ApplyingBuffsMemory>();
        memory.Forget<InquiringMindAttemptedMemory>();
        if (IllegalModeActivityWork.TryRememberPreBuffJob(memory, jobs))
        {
            if (jobs.TryGetCurrent(out SupportJob latched))
            {
                logger.Debug("Illegal Mode buff: latched restore job {Job}", latched.Id);
            }
        }
        else if (IllegalModeActivityWork.TryGetPendingJobRestore(memory, out SupportJobId existing))
        {
            logger.Debug(
                "Illegal Mode buff: kept existing restore latch {Job} (current {Current})",
                existing,
                jobs.TryGetCurrent(out SupportJob cur) ? cur.Id.ToString() : "?");
        }
        else if (jobs.TryGetCurrent(out SupportJob current)
                 && IllegalModeActivityWork.IsBuffSwapJob(current.Id))
        {
            logger.Debug(
                "Illegal Mode buff: no restore latch (already on temporary job {Job})",
                current.Id);
        }
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        // Buff SM finished or we were pre-empted — drop the latch so Choosing/Pathfinding can run.
        if (next != AutomatorState.ApplyingBuffs)
        {
            ClearBuffLatch();
        }
    }

    public override void Handle()
    {
        if (stateMachine == null)
        {
            return;
        }

        stateMachine.Update();

        // Manual BuffRunner aborts on NoCrystalsFound; Illegal Mode must clear the latch too.
        if (stateMachine.State == BuffState.NoCrystalsFound)
        {
            logger.Warning("Illegal Mode buff run aborted — no knowledge crystals nearby");
            pathfinder.Stop();
            ClearBuffLatch();
            stateMachine = null;
        }
    }

    public override void Render()
    {
        stateMachine?.Render();
    }

    private void ClearBuffLatch()
    {
        memory.Forget<ApplyingBuffsMemory>();
        memory.Forget<InquiringMindAttemptedMemory>();
    }
}
