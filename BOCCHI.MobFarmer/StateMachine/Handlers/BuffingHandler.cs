using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Data;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class BuffingHandler
(
    MobFarmerConfig config,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Buffing)
{
    private bool castBattleBell;

    private SupportJobId? jobToRestore;

    public override void Enter()
    {
        base.Enter();
        castBattleBell = false;
        jobToRestore = null;

        if (supportJobs.TryGetCurrent(out SupportJob job)
            && job.Id != SupportJobId.PhantomGeomancer)
        {
            jobToRestore = job.Id;
        }
    }

    public override FarmerPhase? Handle()
    {
        if (!config.ApplyBattleBell)
        {
            return RestoreThenGather();
        }

        if (Actions.PhantomActionI.GetRecastTime() > config.MaximumBattleBellWaitTime)
        {
            return RestoreThenGather();
        }

        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            if (!conditions[ConditionFlag.Mounting])
            {
                Actions.Dismount.Cast();
            }

            return null;
        }

        // Only force Geo while we still need to cast Bell — after that, restore must win (#94).
        if (!castBattleBell)
        {
            if (!IsGeomancer())
            {
                if (!changer.IsBusy())
                {
                    changer.Change(SupportJobId.PhantomGeomancer);
                }

                return null;
            }

            if (Actions.PhantomActionI.CanCast())
            {
                Actions.PhantomActionI.Cast();
                castBattleBell = true;
            }

            return null;
        }

        if (Actions.Sprint.CanCast())
        {
            Actions.Sprint.Cast();
        }

        return RestoreThenGather();
    }

    private FarmerPhase? RestoreThenGather()
    {
        if (jobToRestore is not { } restoreId)
        {
            return FarmerPhase.Gathering;
        }

        if (supportJobs.TryGetCurrent(out SupportJob current) && current.Id == restoreId)
        {
            jobToRestore = null;
            return FarmerPhase.Gathering;
        }

        if (!changer.IsBusy())
        {
            changer.Change(restoreId);
        }

        return null;
    }

    private bool IsGeomancer() =>
        supportJobs.TryGetCurrent(out SupportJob job) && job.Id == SupportJobId.PhantomGeomancer;
}
