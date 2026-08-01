using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Extensions;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.Extensions;
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

        if (GetMinutesRemainingForLowestBuff(player) >= 29)
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

    private uint GetMinutesRemainingForLowestBuff(IPlayerCharacter player)
    {
        uint lowest = 30;
        foreach(BuffData buff in buffs.GetBuffs())
        {
            if (!player.StatusList.TryGet(buff.StatusId, out IStatus _))
            {
                return 0;
            }

            uint time = player.GetRemainingMinutes(buff.StatusId);
            if (time < lowest)
            {
                lowest = time;
            }
        }

        return lowest;
    }
}
