using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Middleware.Chain;
using Ocelot.Chain.Middleware.Step;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

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
                    // Zone data may be stale at compose time.
                    AethernetData? target = zones.GetZone().FindAetheryte(placeNameId);
                    if (objects.LocalPlayer is { } player
                        && AetheryteApproach.IsAlreadyAtAetheryte(target, player.Position))
                    {
                        logger.Info("Already at aetheryte {Id} — skipping teleport", placeNameId);
                        return StepResult.Break();
                    }

                    return StepResult.Success();
                }, $"{chainName}::SkipIfAlreadyThere")
            .Then(AetheryteApproach.BuildApproachChain(
                chains,
                zones.GetZone(),
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

                    if (!AetheryteApproach.IsReadyForLifestream(zones.GetZone(), lifestream, player.Position))
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
                AethernetData? target = zones.GetZone().FindAetheryte(placeNameId);
                if (objects.LocalPlayer is { } player
                    && AetheryteApproach.IsAlreadyAtAetheryte(target, player.Position))
                {
                    logger.Info("Already at destination aetheryte {Id} — skip Lifestream call", placeNameId);
                    return StepResult.Success();
                }

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

                    AethernetData? target = zones.GetZone().FindAetheryte(placeNameId);
                    return ValueTask.FromResult(AetheryteApproach.IsAlreadyAtAetheryte(target, player.Position));
                },
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(250),
                $"{chainName}::WaitUntilArrived")
            .Wait(TimeSpan.FromMilliseconds(500));
    }
}
