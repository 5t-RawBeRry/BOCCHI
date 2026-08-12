using BOCCHI.Common.Data.SupportJobs;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Middleware.Chain;
using Ocelot.Chain.Middleware.Step;
using Ocelot.Extensions;

namespace BOCCHI.Treasure.ChainRecipes;

/// <summary>
///     Dismount → Freelancer → Treasure Sight (Phantom Action II) → restore previous phantom job.
/// </summary>
public class HuntTreasureSightChain
(
    IChainFactory chains,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    IObjectTable objects
) : ChainRecipe(chains)
{
    public override string Name => "Hunt Treasure Sight";

    protected override IChain Compose(IChain chain)
    {
        SupportJobId? restoreId = null;
        if (supportJobs.TryGetCurrent(out SupportJob current)
            && current.Id != SupportJobId.PhantomFreelancer)
        {
            restoreId = current.Id;
        }

        SupportJob freelancer = supportJobs.Create(SupportJobId.PhantomFreelancer);
        var castState = new CastState();

        return chain
            .UseMiddleware<LogChainMiddleware>()
            .UseStepMiddleware<LogStepMiddleware>()
            .UseStepMiddleware<RunOnMainThreadMiddleware>()
            .IfThen(
                _ => !SupportJobTreasureSight.CanCast(supportJobs),
                _ => ValueTask.FromResult(StepResult.Break()),
                "HuntTreasureSight::FreelancerTooLow"
            )
            .WaitUntil(
                _ => ValueTask.FromResult(TryDismount()),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                "HuntTreasureSight::Dismount"
            )
            .WaitUntil(
                _ => ValueTask.FromResult(TryBecomeJob(SupportJobId.PhantomFreelancer, freelancer.StatusId)),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                "HuntTreasureSight::ToFreelancer"
            )
            .WaitUntil(
                _ => ValueTask.FromResult(TryCastSight(castState)),
                TimeSpan.FromSeconds(20),
                TimeSpan.FromMilliseconds(250),
                "HuntTreasureSight::Cast"
            )
            .WaitUntil(
                _ => ValueTask.FromResult(TryRestore(restoreId)),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                "HuntTreasureSight::RestoreJob"
            );
    }

    private bool TryDismount()
    {
        if (!conditions[ConditionFlag.Mounted] && !conditions[ConditionFlag.Mounting])
        {
            return true;
        }

        if (!conditions[ConditionFlag.Mounting]
            && EzThrottler.Throttle("HuntTreasureSight::Dismount", 500)
            && Actions.Dismount.CanCast())
        {
            Actions.Dismount.Cast();
        }

        return false;
    }

    private bool TryBecomeJob(SupportJobId id, uint statusId)
    {
        if (objects.LocalPlayer is not { } player)
        {
            return false;
        }

        if (supportJobs.TryGetCurrent(out SupportJob current) && current.Id == id)
        {
            return true;
        }

        if (!EzThrottler.Throttle($"HuntTreasureSight::Change::{id}", 750))
        {
            return false;
        }

        unsafe
        {
            PublicContentOccultCrescent.ChangeSupportJob((byte)id);
        }

        return player.StatusList.Has(statusId);
    }

    /// <summary>
    /// Start Treasure Sight and wait until the cast finishes.
    /// Returning true on UseAction alone restored the previous job mid-cast and cancelled Sight.
    /// </summary>
    private bool TryCastSight(CastState state)
    {
        if (IsCasting())
        {
            state.SawCasting = true;
            return false;
        }

        if (state.SawCasting || state.Issued)
        {
            // Cast completed (or never entered casting for an instant-style success).
            return true;
        }

        if (!EzThrottler.Throttle("HuntTreasureSight::Cast", 500))
        {
            return false;
        }

        if (!Actions.PhantomActionII.CanCast())
        {
            return false;
        }

        if (Actions.PhantomActionII.Cast())
        {
            state.Issued = true;
        }

        return false;
    }

    private bool IsCasting() =>
        conditions[ConditionFlag.Casting] || conditions[ConditionFlag.Casting87];

    private bool TryRestore(SupportJobId? restoreId)
    {
        // Never swap while still casting — job change cancels Treasure Sight.
        if (IsCasting())
        {
            return false;
        }

        if (restoreId is not { } id)
        {
            return true;
        }

        SupportJob job = supportJobs.Create(id);
        return TryBecomeJob(id, job.StatusId);
    }

    private sealed class CastState
    {
        public bool Issued;

        public bool SawCasting;
    }
}
