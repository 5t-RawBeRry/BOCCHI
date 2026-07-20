using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Middleware.Chain;
using Ocelot.Chain.Middleware.Step;
using Ocelot.Extensions;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;
namespace BOCCHI.Common.Data.Aethernet;

public static class AethernetTeleport
{
    public static IChain BuildChain(
        IChain chain,
        IChainFactory chains,
        IZoneProvider zones,
        IObjectTable objects,
        IPathfinder pathfinder,
        IVNavmeshIpc vnav,
        ILifestreamIpc lifestream,
        ILogger logger,
        uint placeNameId)
    {
        string chainName = chain.Name;
        IZone zone = zones.GetZone();
        AethernetData? aetheryte = zone.FindAetheryte(placeNameId);
        Vector3 arrivalPosition = aetheryte?.GetInteractPosition() ?? aetheryte?.Position ?? Vector3.Zero;
        float arrivalRadius = aetheryte?.DeadRadius ?? 5f;

        return chain
            .UseMiddleware<LogChainMiddleware>()
            .UseMiddleware(new RetryChainMiddleware(logger)
            {
                DelayMs = 500,
                MaxAttempts = 5
            })
            .UseStepMiddleware<LogStepMiddleware>()
            .UseStepMiddleware<RunOnMainThreadMiddleware>()
            .Then(_ =>
                {
                    if (objects.LocalPlayer is { } player
                        && AetheryteApproach.IsAlreadyAtAetheryte(aetheryte, player.Position))
                    {
                        logger.Info("Already at aetheryte {Id} — skipping teleport", placeNameId);
                        return StepResult.Break();
                    }

                    return StepResult.Success();
                }, $"{chainName}::SkipIfAlreadyThere")
            .Then(AetheryteApproach.BuildApproachChain(
                chains,
                zone,
                objects,
                pathfinder,
                vnav,
                lifestream,
                $"{chainName}::Approach"))
            .Then(_ =>
                {
                    if (objects.LocalPlayer is not { } player)
                    {
                        return StepResult.Failure("No local player.");
                    }

                    if (!AetheryteApproach.IsReadyForLifestream(zone, lifestream, player.Position))
                    {
                        return StepResult.Failure("Not close enough to an aetheryte for Lifestream.");
                    }

                    return StepResult.Success();
                }, $"{chainName}::VerifyAetheryteRange")
            .WaitUntil(
                _ => ValueTask.FromResult(!lifestream.IsBusy()),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(250),
                $"{chainName}::WaitUntilLifestreamIsFree")
            .Then(_ =>
            {
                if (!lifestream.AethernetTeleportByPlaceNameId(placeNameId))
                {
                    return StepResult.Failure("Lifestream rejected aethernet teleport.");
                }

                return StepResult.Success();
            }, $"{chainName}::Teleport")
            .WaitUntil(
                _ =>
                {
                    if (lifestream.IsBusy())
                    {
                        return ValueTask.FromResult(false);
                    }

                    if (objects.LocalPlayer is not { } player)
                    {
                        return ValueTask.FromResult(false);
                    }

                    return ValueTask.FromResult(player.Position.Distance2D(arrivalPosition) <= arrivalRadius);
                },
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                $"{chainName}::WaitUntilArrived")
            .Wait(TimeSpan.FromMilliseconds(500));
    }
}
