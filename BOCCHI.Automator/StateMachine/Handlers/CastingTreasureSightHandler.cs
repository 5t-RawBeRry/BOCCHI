using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
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
    AutomatorConfig config
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.CastingTreasureSight)
{
    private DateTime lastCast = DateTime.MinValue;

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<CastingTreasureSightMemory>(out CastingTreasureSightMemory _))
        {
            return StatePriority.MediumHigh;
        }

        if (memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
        {
            return StatePriority.Never;
        }

        SupportJob freelancer = supportJobs.Create(SupportJobId.PhantomFreelancer);
        if (freelancer.Level < 10)
        {
            return StatePriority.Never;
        }

        if (zone.GetZone().IsInBasecamp() && config.ShouldCastTreasureSight && GetLastCastDeltaSeconds() >= config.TreasureSightRecastIntervalSeconds)
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

        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            if (!conditions[ConditionFlag.Mounting])
            {
                Actions.Dismount.Cast();
            }

            return;
        }

        if (current.Id != SupportJobId.PhantomFreelancer)
        {
            if (!changer.IsBusy())
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
