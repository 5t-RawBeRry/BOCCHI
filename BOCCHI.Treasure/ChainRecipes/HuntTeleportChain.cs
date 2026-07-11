using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Middleware.Chain;
using Ocelot.Chain.Middleware.Step;
using Ocelot.Ipc.BossMod;
using Ocelot.Services.Logger;

namespace BOCCHI.Treasure.ChainRecipes;

public class HuntTeleportChain(
    IChainFactory chains,
    ILifestreamIpc lifestream,
    ICondition conditions,
    ILogger<HuntTeleportChain> logger
) : ChainRecipe<uint>(chains)
{
    public override string Name { get; } = "Hunt Teleport Chain";

    protected override IChain Compose(IChain chain, uint placeNameId)
    {
        return chain
            .UseMiddleware<LogChainMiddleware>()
            .UseMiddleware(new RetryChainMiddleware(logger)
            {
                DelayMs = 500,
                MaxAttempts = 5,
            })
            .UseStepMiddleware<LogStepMiddleware>()
            .UseStepMiddleware<RunOnMainThreadMiddleware>()
            .WaitUntil(
                _ => ValueTask.FromResult(!lifestream.IsBusy()),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(250),
                "HuntTeleportChain::WaitUntilLifestreamIsFree"
            )
            .Then(
                _ => lifestream.AethernetTeleportByPlaceNameId(placeNameId),
                "HuntTeleportChain::Teleport"
            )
            .WaitUntil(
                _ => ValueTask.FromResult(conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(250),
                "HuntTeleportChain::WaitUntilBetweenAreas"
            )
            .WaitUntil(
                _ => ValueTask.FromResult(!conditions[ConditionFlag.BetweenAreas] && !conditions[ConditionFlag.BetweenAreas51]),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(250),
                "HuntTeleportChain::WaitUntilNotBetweenAreas"
            )
            .Wait(TimeSpan.FromMilliseconds(500));
    }
}
