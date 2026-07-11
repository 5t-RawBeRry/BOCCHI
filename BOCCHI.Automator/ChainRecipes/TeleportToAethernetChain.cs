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

namespace BOCCHI.Automator.ChainRecipes;

public class TeleportToAethernetChain(
    IChainFactory chains,
    ILifestreamIpc lifestream,
    IZoneProvider zones,
    IObjectTable objects,
    ILogger<TeleportToAethernetChain> logger
) : ChainRecipe<uint>(chains)
{
    public override string Name { get; } = "Teleport to Aethernet Chain";

    protected override IChain Compose(IChain chain, uint id)
    {
        var aetheryte = zones.GetZone().FindAetheryte(id);
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
                    "TeleportToAethernetChain::WaitUntilLifestreamIsFree"
                )
                .Then(_ =>
                {
                    if (!lifestream.AethernetTeleportByPlaceNameId(id))
                    {
                        return StepResult.Failure("Lifestream rejected aethernet teleport.");
                    }

                    return StepResult.Success();
                }, "TeleportToAethernetChain::Teleport")
                .WaitUntil(
                    _ => ValueTask.FromResult(HasArrived(arrivalPosition, arrivalRadius)),
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromMilliseconds(250),
                    "TeleportToAethernetChain::WaitUntilArrived"
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
