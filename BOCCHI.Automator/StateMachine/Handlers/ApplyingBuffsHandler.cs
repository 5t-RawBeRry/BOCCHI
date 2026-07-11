using BOCCHI.Automator.Data;
using BOCCHI.Automator.Data.StateMemory;
using BOCCHI.Automator.Services;
using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Ocelot.Extensions;
using Ocelot.States;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ApplyingBuffsHandler(
    Func<IStateMachine<BuffState>> factory,
    IBuffProvider buffs,
    IZoneProvider zones,
    IAutomatorMemory memory,
    ISupportJobFactory jobs,
    BuffConfig config
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.ApplyingBuffs)
{
    private IStateMachine<BuffState>? stateMachine;

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<ApplyingBuffsMemory>(out var _))
        {
            return StatePriority.VeryHigh;
        }

        if (!config.ShouldAutomateBuffs || !buffs.ShouldRefreshAny())
        {
            return StatePriority.Never;
        }

        var zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            return StatePriority.Never;
        }

        if (!zone.GetNearbyKnowledgeCrystals().Any())
        {
            return StatePriority.Never;
        }

        return StatePriority.MediumHigh;
    }

    public override void Enter()
    {
        stateMachine = factory();

        memory.TryAdd<ApplyingBuffsMemory>();
        if (jobs.TryGetCurrent(out var job))
        {
            memory.TryAdd(new BuffSupportJobMemory(job.Id));
        }
    }

    public override void Handle()
    {
        stateMachine?.Update();
    }

    public override void Render()
    {
        stateMachine?.Render();
    }
}
