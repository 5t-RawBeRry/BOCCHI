using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.States.Flow;

namespace BOCCHI.Buff.StateMachine.Handlers;

public class CastingInquiringMindHandler
(
    IObjectTable objects,
    ISupportJobChanger changer,
    ISupportJobFactory supportJobs,
    IBuffProvider buffs
) : FlowStateHandler<BuffState>(BuffState.CastingInquiringMind)
{
    private DateTime lastCast = DateTime.MinValue;

    public override void Enter()
    {
        base.Enter();
        lastCast = DateTime.MinValue;
    }

    public override BuffState? Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return null;
        }

        // Success = Quicker Step refreshed (what Inquiring Mind actually applies).
        if (buffs.IsInquiringMindFresh(player))
        {
            return BuffState.ChoosingBuffToApply;
        }

        if (!supportJobs.TryGetCurrent(out SupportJob supportJob) || supportJob.Id != SupportJobId.PhantomFreelancer)
        {
            if (!changer.IsBusy())
            {
                changer.Change(SupportJobId.PhantomFreelancer);
            }

            return null;
        }

        TimeSpan time = DateTime.UtcNow - lastCast;
        if (Actions.PhantomActionIII.CanCast() && time.TotalSeconds >= 3)
        {
            lastCast = DateTime.UtcNow;
            Actions.PhantomActionIII.Cast();
        }

        return null;
    }
}
