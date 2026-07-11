using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Middleware.Chain;
using Ocelot.Chain.Middleware.Step;
using Ocelot.Extensions;
using Ocelot.Ipc.BossMod;
using Ocelot.Services.Logger;

namespace BOCCHI.Treasure.ChainRecipes;

public class HuntTeleportChain(
    IChainFactory chains,
    ILifestreamIpc lifestream,
    IZoneProvider zones,
    IObjectTable objects,
    ILogger<HuntTeleportChain> logger
) : ChainRecipe<uint>(chains)
{
    public override string Name { get; } = "Hunt Teleport Chain";

    protected override IChain Compose(IChain chain, uint placeNameId)
    {
        var aetheryte = zones.GetZone().FindAetheryte(placeNameId);
        var arrivalPosition = aetheryte?.Destination ?? aetheryte?.Position ?? System.Numerics.Vector3.Zero;
        var arrivalRadius = aetheryte?.DeadRadius ?? 5f;

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
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(250),
                "HuntTeleportChain::WaitUntilLifestreamIsFree"
            )
            .Then(_ =>
            {
                if (!lifestream.AethernetTeleportByPlaceNameId(placeNameId))
                {
                    return StepResult.Failure("Lifestream rejected aethernet teleport.");
                }

                return StepResult.Success();
            }, "HuntTeleportChain::Teleport")
            .WaitUntil(
                _ => ValueTask.FromResult(HasArrived(arrivalPosition, arrivalRadius)),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                "HuntTeleportChain::WaitUntilArrived"
            )
            .Wait(TimeSpan.FromMilliseconds(500));
    }

    private bool HasArrived(System.Numerics.Vector3 destination, float radius)
    {
        if (lifestream.IsBusy())
        {
            return false;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return false;
        }

        return player.Position.Distance2D(destination) <= radius;
    }
}
