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

        return chain
            .UseMiddleware<LogChainMiddleware>()
            .UseStepMiddleware<LogStepMiddleware>()
            .UseStepMiddleware<RunOnMainThreadMiddleware>()
            .IfThen(
                _ => freelancer.Level < 10,
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
                _ => ValueTask.FromResult(TryCastSight()),
                TimeSpan.FromSeconds(15),
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

    private bool TryCastSight()
    {
        if (!EzThrottler.Throttle("HuntTreasureSight::Cast", 500))
        {
            return false;
        }

        if (!Actions.PhantomActionII.CanCast())
        {
            return false;
        }

        return Actions.PhantomActionII.Cast();
    }

    private bool TryRestore(SupportJobId? restoreId)
    {
        if (restoreId is not { } id)
        {
            return true;
        }

        SupportJob job = supportJobs.Create(id);
        return TryBecomeJob(id, job.StatusId);
    }
}
