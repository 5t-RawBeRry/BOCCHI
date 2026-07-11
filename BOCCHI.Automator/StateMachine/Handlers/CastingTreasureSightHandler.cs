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

public class CastingTreasureSightHandler(
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
        if (memory.TryRemember<CastingTreasureSightMemory>(out var _))
        {
            return StatePriority.MediumHigh;
        }

        if (memory.TryRemember<ApplyingBuffsMemory>(out _))
        {
            return StatePriority.Never;
        }

        var freelancer = supportJobs.Create(SupportJobId.PhantomFreelancer);
        if (freelancer.Level < 10)
        {
            return StatePriority.Never;
        }

        if (zone.GetZone().IsInBasecamp() && config.ShouldCastTreasureSight  && GetLastCastDeltaSeconds() >= config.TreasureSightRecastIntervalSeconds)
        {
            return StatePriority.Always;
        }

        return StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();

        if (supportJobs.TryGetCurrent(out var current))
        {
            memory.TryAdd(new TreasureSightSupportJobMemory(current.Id));
        }

        memory.TryAdd<CastingTreasureSightMemory>();
    }

    public override void Handle()
    {
        if (!supportJobs.TryGetCurrent(out var current))
        {
            return;
        }

        if (conditions[ConditionFlag.Mounted])
        {
            if (Actions.Dismount.CanCast())
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
            }
        }
    }

    private int GetLastCastDeltaSeconds()
    {
        return (DateTime.Now - lastCast).Seconds;
    }
}
