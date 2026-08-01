using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Extensions;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.States.Flow;

namespace BOCCHI.Buff.StateMachine.Handlers;

public class ChoosingBuffToApplyHandler
(
    BuffConfig config,
    IObjectTable objects,
    IBuffProvider buffs,
    IAutomatorMemory memory,
    ISupportJobFactory supportJobs
) : FlowStateHandler<BuffState>(BuffState.ChoosingBuffToApply)
{
    private const uint ManualFreshEnoughMinutes = 25;

    public override BuffState? Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return null;
        }

        bool forceRefresh = memory.TryRemember<ManualBuffRunMemory>(out ManualBuffRunMemory _);
        SupportJob freelancer = supportJobs.Create(SupportJobId.PhantomFreelancer);

        if (config.ApplyBuffsUsingInquiringMind
            && freelancer.Level >= 15
            && GetLowestEnabledBuffMinutes(player) < (forceRefresh ? ManualFreshEnoughMinutes : 29))
        {
            return BuffState.CastingInquiringMind;
        }

        foreach(BuffData buff in buffs.GetBuffs().Where(b => b.ShouldApply(config)))
        {
            SupportJob job = supportJobs.Create(buff.SupportJobId);
            if (job.Level < buff.RequiredLevel)
            {
                continue;
            }

            uint remaining = player.GetRemainingMinutes(buff.StatusId);
            uint skipAbove = forceRefresh ? ManualFreshEnoughMinutes : (uint)config.ReapplyThreshold;
            if (remaining > skipAbove)
            {
                continue;
            }

            return buff.State;
        }

        memory.Forget<ApplyingBuffsMemory>();
        memory.Forget<ManualBuffRunMemory>();

        return null;
    }

    private uint GetLowestEnabledBuffMinutes(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter player)
    {
        uint lowest = 30;
        bool any = false;

        foreach(BuffData buff in buffs.GetBuffs().Where(b => b.ShouldApply(config)))
        {
            SupportJob job = supportJobs.Create(buff.SupportJobId);
            if (job.Level < buff.RequiredLevel)
            {
                continue;
            }

            any = true;
            uint time = player.GetRemainingMinutes(buff.StatusId);
            if (time < lowest)
            {
                lowest = time;
            }
        }

        return any ? lowest : 30;
    }
}
