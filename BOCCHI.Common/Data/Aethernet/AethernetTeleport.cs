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
                    IZone zone = zones.GetZone();
                    if (objects.LocalPlayer is { } player
                        && AetheryteApproach.IsAtPlaceName(zone, placeNameId, player.Position))
                    {
                        if (lifestream.IsBusy())
                        {
                            lifestream.Abort();
                        }

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

                    // Arrived during approach (or TP landed) — don't open Lifestream again.
                    if (AetheryteApproach.IsAtPlaceName(zones.GetZone(), placeNameId, player.Position))
                    {
                        if (lifestream.IsBusy())
                        {
                            lifestream.Abort();
                        }

                        logger.Info("Arrived at aetheryte {Id} during approach — skipping teleport", placeNameId);
                        return StepResult.Break();
                    }

                    if (!AetheryteApproach.IsReadyForLifestream(zones.GetZone(), lifestream, player.Position))
                    {
                        return StepResult.Failure("Not close enough to an aetheryte for Lifestream.");
                    }

                    return StepResult.Success();
                }, $"{chainName}::VerifyAetheryteRange")
            .Then(_ =>
                {
                    // Stuck destination overlay / leftover task blocks AethernetTeleport (returns false when busy).
                    if (lifestream.IsBusy())
                    {
                        logger.Info("Lifestream busy before teleport — aborting leftover task");
                        lifestream.Abort();
                    }

                    return StepResult.Success();
                }, $"{chainName}::AbortIfBusy")
            .WaitUntil(
                _ => ValueTask.FromResult(!lifestream.IsBusy()),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(250),
                $"{chainName}::WaitUntilLifestreamIsFree")
            .Then(_ =>
            {
                IZone zone = zones.GetZone();
                if (objects.LocalPlayer is { } player
                    && AetheryteApproach.IsAtPlaceName(zone, placeNameId, player.Position))
                {
                    if (lifestream.IsBusy())
                    {
                        lifestream.Abort();
                    }

                    logger.Info("Already at destination aetheryte {Id} — skip Lifestream call", placeNameId);
                    return StepResult.Success();
                }

                if (!lifestream.AethernetTeleportByPlaceNameId(placeNameId))
                {
                    lifestream.Abort();
                    return StepResult.Failure("Lifestream rejected aethernet teleport.");
                }

                return StepResult.Success();
            }, $"{chainName}::Teleport")
            // Confirm Lifestream actually started; silent no-ops used to burn the full arrive timeout.
            .WaitUntil(
                _ =>
                {
                    if (objects.LocalPlayer is { } player
                        && AetheryteApproach.IsAtPlaceName(zones.GetZone(), placeNameId, player.Position))
                    {
                        return ValueTask.FromResult(true);
                    }

                    return ValueTask.FromResult(lifestream.IsBusy());
                },
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(200),
                $"{chainName}::WaitUntilTeleportStarted")
            .WaitUntil(
                _ =>
                {
                    if (objects.LocalPlayer is not { } player)
                    {
                        return ValueTask.FromResult(false);
                    }

                    // Arrived at target shard — close the aethernet menu so we don't stall on IsBusy.
                    if (!AetheryteApproach.IsAtPlaceName(zones.GetZone(), placeNameId, player.Position))
                    {
                        return ValueTask.FromResult(false);
                    }

                    if (lifestream.IsBusy())
                    {
                        lifestream.Abort();
                    }

                    return ValueTask.FromResult(true);
                },
                TimeSpan.FromSeconds(20),
                TimeSpan.FromMilliseconds(250),
                $"{chainName}::WaitUntilArrived")
            .Then(_ =>
                {
                    if (lifestream.IsBusy())
                    {
                        lifestream.Abort();
                    }

                    return StepResult.Success();
                }, $"{chainName}::AbortIfStillBusy")
            .Wait(TimeSpan.FromMilliseconds(500));
    }
}

