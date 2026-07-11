using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Extensions;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;

namespace BOCCHI.Buff.StateMachine.Handlers;

public class CastingInquiringMindHandler(
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

        if (!supportJobs.TryGetCurrent(out var supportJob) || supportJob.Id != SupportJobId.PhantomFreelancer)
        {
            if (!changer.IsBusy())
            {
                changer.Change(SupportJobId.PhantomFreelancer);
            }

            return null;
        }

        var time = DateTime.UtcNow - lastCast;
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
        foreach (var buff in buffs.GetBuffs())
        {
            if (!player.StatusList.TryGet(buff.StatusId, out _))
            {
                return 0;
            }

            var time = player.GetRemainingMinutes(buff.StatusId);
            if (time < lowest)
            {
                lowest = time;
            }
        }

        return lowest;
    }
}
