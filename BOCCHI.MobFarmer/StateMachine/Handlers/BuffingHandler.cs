using BOCCHI.Common.Config;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.SupportJobs;
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
    /// <summary>Battle Bell by action ID — not the current job's Phantom Action I slot.</summary>
    private static readonly Action BattleBell = new(ActionType.Action, PhantomActions.BattleBell);

    private static readonly TimeSpan SprintGiveUp = TimeSpan.FromSeconds(2.5);

    private bool castBattleBell;

    private bool sprintDone;

    private DateTimeOffset? sprintWaitStartedUtc;

    private SupportJobId? jobToRestore;

    public override void Enter()
    {
        base.Enter();
        castBattleBell = false;
        sprintDone = false;
        sprintWaitStartedUtc = null;
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

        // Gate on Battle Bell's own CD, not whatever Action I the combat job has equipped (#103).
        if (BattleBell.GetRecastTime() > config.MaximumBattleBellWaitTime)
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

        // Reapply every pull when enabled (#145) — do not skip just because Bell/Clangor is still up.
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

            // Wait out remaining CD (already below MaximumBattleBellWaitTime).
            if (!Actions.PhantomActionI.CanCast())
            {
                return null;
            }

            Actions.PhantomActionI.Cast();
            castBattleBell = true;
            return null;
        }

        // Action must have been consumed (covers refresh while buff still ticking).
        if (BattleBell.GetRecastTime() <= 0f)
        {
            if (IsGeomancer() && Actions.PhantomActionI.CanCast())
            {
                Actions.PhantomActionI.Cast();
            }

            return null;
        }

        // Don't swap jobs until the buff actually sticks (#103).
        if (!HasBattleBell())
        {
            return null;
        }

        return TrySprintThenGather();
    }

    private FarmerPhase? TrySprintThenGather()
    {
        if (!sprintDone)
        {
            sprintWaitStartedUtc ??= DateTimeOffset.UtcNow;

            if (Actions.Sprint.CanCast())
            {
                Actions.Sprint.Cast();
                return null;
            }

            // CanCast false: on CD (cast landed) or still animation-locked after Bell — keep waiting a bit.
            bool sprintOnCooldown = Actions.Sprint.GetRecastTime() > 0f;
            bool timedOut = DateTimeOffset.UtcNow - sprintWaitStartedUtc >= SprintGiveUp;
            if (!sprintOnCooldown && !timedOut)
            {
                return null;
            }

            sprintDone = true;
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

    private bool HasBattleBell()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return false;
        }

        return player.StatusList.Has(PhantomBuffs.BattleBell)
               || player.StatusList.Has(PhantomBuffs.BattlesClangor);
    }
}
