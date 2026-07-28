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
    public override BuffState? Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return null;
        }

        SupportJob freelancer = supportJobs.Create(SupportJobId.PhantomFreelancer);
        if (config.ApplyBuffsUsingInquiringMind && freelancer.Level >= 15)
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

            if (player.GetRemainingMinutes(buff.StatusId) > config.ReapplyThreshold)
            {
                continue;
            }

            return buff.State;
        }

        memory.Forget<ApplyingBuffsMemory>();

        return null;
    }
}
