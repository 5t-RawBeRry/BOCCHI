using BOCCHI.Common.Config;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Extensions;
using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Data;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.States.Flow;
using Action = Ocelot.Actions.Action;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class BuffingHandler
(
    MobFarmerConfig config,
    ICondition conditions,
    IObjectTable objects,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Buffing)
{
    private static readonly Action BattleBell = new(ActionType.Action, PhantomActions.BattleBell);

    private static readonly Action RingingRespite = new(ActionType.Action, PhantomActions.RingingRespite);

    private static readonly TimeSpan StepGiveUp = TimeSpan.FromSeconds(2.5);

    private bool quickstepDone;

    private bool bellDone;

    private bool respiteDone;

    private bool sprintDone;

    private DateTimeOffset? stepWaitStartedUtc;

    private SupportJobId? jobToRestore;

    public override void Enter()
    {
        base.Enter();
        quickstepDone = false;
        bellDone = false;
        respiteDone = false;
        sprintDone = false;
        stepWaitStartedUtc = null;
        jobToRestore = null;

        if (supportJobs.TryGetCurrent(out SupportJob job))
        {
            jobToRestore = job.Id;
        }
    }

    public override FarmerPhase? Handle()
    {
        if (DismountAssist.TryDismount(conditions))
        {
            return null;
        }

        if (!quickstepDone)
        {
            FarmerPhase? quickstep = TryQuickstep();
            if (quickstep == null && !quickstepDone)
            {
                return null;
            }
        }

        if (!bellDone || !respiteDone)
        {
            FarmerPhase? geo = TryGeomancerBuffs();
            if (geo == null && (!bellDone || !respiteDone))
            {
                return null;
            }
        }

        return TrySprintThenGather();
    }

    private FarmerPhase? TryQuickstep()
    {
        if (!config.ApplyQuickstep || supportJobs.Create(SupportJobId.PhantomDancer).Level < 2)
        {
            quickstepDone = true;
            return FarmerPhase.Gathering;
        }

        if (config.QuickstepSkipIfRemainingMinutes > 0
            && objects.LocalPlayer is { } local
            && local.GetRemainingMinutes(PhantomBuffs.QuickerStep) >= (uint)config.QuickstepSkipIfRemainingMinutes)
        {
            quickstepDone = true;
            return FarmerPhase.Gathering;
        }

        if (!IsJob(SupportJobId.PhantomDancer))
        {
            if (!changer.IsBusy())
            {
                changer.Change(SupportJobId.PhantomDancer);
            }

            return null;
        }

        if (Actions.PhantomActionII.CanCast())
        {
            Actions.PhantomActionII.Cast();
            stepWaitStartedUtc = DateTimeOffset.UtcNow;
            return null;
        }

        if (HasQuickstepBuff() || DateTimeOffset.UtcNow - (stepWaitStartedUtc ?? DateTimeOffset.UtcNow) >= StepGiveUp)
        {
            quickstepDone = true;
            stepWaitStartedUtc = null;
            return FarmerPhase.Gathering;
        }

        return null;
    }

    private FarmerPhase? TryGeomancerBuffs()
    {
        bool wantBell = config.ApplyBattleBell && BattleBell.GetRecastTime() <= config.MaximumBattleBellWaitTime;
        bool wantRespite = config.ApplyRingingRespite
                           && supportJobs.Create(SupportJobId.PhantomGeomancer).Level >= 3
                           && RingingRespite.GetRecastTime() <= config.MaximumBattleBellWaitTime;

        if (!wantBell)
        {
            bellDone = true;
        }

        if (!wantRespite)
        {
            respiteDone = true;
        }

        if (bellDone && respiteDone)
        {
            return FarmerPhase.Gathering;
        }

        if (!IsJob(SupportJobId.PhantomGeomancer))
        {
            if (!changer.IsBusy())
            {
                changer.Change(SupportJobId.PhantomGeomancer);
            }

            return null;
        }

        if (!bellDone)
        {
            if (BattleBell.GetRecastTime() <= 0f && Actions.PhantomActionI.CanCast())
            {
                Actions.PhantomActionI.Cast();
                return null;
            }

            if (!HasBattleBell())
            {
                return null;
            }

            bellDone = true;
        }

        if (!respiteDone)
        {
            if (RingingRespite.GetRecastTime() <= 0f && Actions.PhantomActionIII.CanCast())
            {
                Actions.PhantomActionIII.Cast();
                stepWaitStartedUtc ??= DateTimeOffset.UtcNow;
                return null;
            }

            if (RingingRespite.GetRecastTime() <= 0f
                && DateTimeOffset.UtcNow - (stepWaitStartedUtc ?? DateTimeOffset.UtcNow) < StepGiveUp)
            {
                return null;
            }

            respiteDone = true;
            stepWaitStartedUtc = null;
        }

        return FarmerPhase.Gathering;
    }

    private FarmerPhase? TrySprintThenGather()
    {
        bool appliedAny = config.ApplyQuickstep || config.ApplyBattleBell || config.ApplyRingingRespite;
        if (!sprintDone && appliedAny)
        {
            stepWaitStartedUtc ??= DateTimeOffset.UtcNow;

            if (Actions.Sprint.CanCast())
            {
                Actions.Sprint.Cast();
                return null;
            }

            bool sprintOnCooldown = Actions.Sprint.GetRecastTime() > 0f;
            bool timedOut = DateTimeOffset.UtcNow - stepWaitStartedUtc >= StepGiveUp;
            if (!sprintOnCooldown && !timedOut)
            {
                return null;
            }

            sprintDone = true;
            stepWaitStartedUtc = null;
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

    private bool IsJob(SupportJobId id) =>
        supportJobs.TryGetCurrent(out SupportJob job) && job.Id == id;

    private bool HasBattleBell()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return false;
        }

        return player.StatusList.Has(PhantomBuffs.BattleBell)
               || player.StatusList.Has(PhantomBuffs.BattlesClangor);
    }

    private bool HasQuickstepBuff()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return false;
        }

        return player.StatusList.Has(PhantomBuffs.QuickerStep);
    }
}
