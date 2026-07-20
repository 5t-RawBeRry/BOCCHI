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

    public override void Enter()
    {
        base.Enter();
        castBattleBell = false;
    }

    public override FarmerPhase? Handle()
    {
        if (!config.ApplyBattleBell)
        {
            return FarmerPhase.Gathering;
        }

        if (Actions.PhantomActionI.GetRecastTime() > config.MaximumBattleBellWaitTime)
        {
            return FarmerPhase.Gathering;
        }

        if (conditions[ConditionFlag.Mounted] && Actions.Dismount.CanCast())
        {
            Actions.Dismount.Cast();
            return null;
        }

        if (!IsGeomancer())
        {
            if (!changer.IsBusy())
            {
                changer.Change(SupportJobId.PhantomGeomancer);
            }

            return null;
        }

        if (!castBattleBell)
        {
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

        return FarmerPhase.Gathering;
    }

    private bool IsGeomancer() => supportJobs.TryGetCurrent(out SupportJob job) && job.Id == SupportJobId.PhantomGeomancer;
}
