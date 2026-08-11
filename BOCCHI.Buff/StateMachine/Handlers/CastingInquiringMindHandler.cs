using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.States.Flow;

namespace BOCCHI.Buff.StateMachine.Handlers;

public class CastingInquiringMindHandler
(
    IObjectTable objects,
    ICondition conditions,
    ISupportJobChanger changer,
    ISupportJobFactory supportJobs,
    IBuffProvider buffs,
    IAutomatorMemory memory
) : FlowStateHandler<BuffState>(BuffState.CastingInquiringMind)
{
    private DateTime lastCast = DateTime.MinValue;
    private int castAttempts;

    public override void Enter()
    {
        base.Enter();
        lastCast = DateTime.MinValue;
        castAttempts = 0;
    }

    public override BuffState? Handle()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return null;
        }

        // Inquiring Mind grants all unlocked crystal buffs in one cast — not Quicker Step only.
        if (buffs.AreInquiringMindTargetsFresh(player) || castAttempts >= 3)
        {
            memory.TryAdd<InquiringMindAttemptedMemory>();
            return BuffState.ChoosingBuffToApply;
        }

        if (DismountAssist.TryDismount(conditions))
        {
            return null;
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
            castAttempts++;
            Actions.PhantomActionIII.Cast();
        }

        return null;
    }
}
