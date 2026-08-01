using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Extensions;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.States.Flow;

namespace BOCCHI.Buff.StateMachine.Handlers;

public class CastingInquiringMindHandler
(
    BuffConfig config,
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

        if (GetMinutesRemainingForLowestEnabledBuff(player) >= 29)
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

    /// <summary>
    ///     Only count buffs that are enabled and unlocked — matching ChoosingBuffToApplyHandler.
    ///     Requiring every BuffData.All entry (e.g. disabled Quickstep) caused an infinite recast loop.
    /// </summary>
    private uint GetMinutesRemainingForLowestEnabledBuff(IPlayerCharacter player)
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
