using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Extensions;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Common.Data.Aethernet;

public static class AetheryteApproach
{
    private static readonly TimeSpan ApproachTimeout = TimeSpan.FromSeconds(20);

    public static IChain BuildApproachChain(
        IChainFactory chains,
        IZone zone,
        IObjectTable objects,
        IPathfinder pathfinder,
        IVNavmeshIpc vnav,
        ILifestreamIpc lifestream,
        string chainName)
    {
        if (objects.LocalPlayer is not { } player)
        {
            return chains.Create(chainName)
                .Then(_ => StepResult.Failure("No local player."), $"{chainName}::NoPlayer");
        }

        // Position may have changed since compose.
        return chains.Create(chainName)
            .Then(_ =>
                {
                    pathfinder.Stop();
                    vnav.Stop();
                    return StepResult.Success();
                }, $"{chainName}::StopMovement")
            .Then((Func<IChainContext, Task<StepResult>>)(async ctx =>
                {
                    if (objects.LocalPlayer is not { } current)
                    {
                        return StepResult.Failure("No local player.");
                    }

                    if (IsReadyForLifestream(zone, lifestream, current.Position))
                    {
                        return StepResult.Success();
                    }

                    AethernetData? nearest = zone.EnumerateAetherytes()
                        .OrderBy(aetheryte => current.Position.Distance2D(aetheryte.Position))
                        .FirstOrDefault();

                    if (nearest == null)
                    {
                        return StepResult.Failure("No aetheryte nearby.");
                    }

                    Vector3 crystal = nearest.Position;
                    Vector3 target = crystal.GetApproachPosition(current.Position, AethernetNavigation.CampApproachRadius);
                    target = new Vector3(target.X, crystal.Y, target.Z);

                    pathfinder.PathfindAndMoveTo(new PathfinderConfig(target)
                    {
                        DistanceThreshold = AethernetNavigation.PathfindArrivalRadius,
                        ShouldSnapToFloor = false,
                    });

                    ChainResult result = await chains.Create($"{chainName}::CloseInWait")
                        .WaitUntil(
                            _ =>
                            {
                                if (objects.LocalPlayer is not { } p)
                                {
                                    return ValueTask.FromResult(false);
                                }

                                if (IsReadyForLifestream(zone, lifestream, p.Position))
                                {
                                    pathfinder.Stop();
                                    vnav.Stop();
                                    return ValueTask.FromResult(true);
                                }

                                return ValueTask.FromResult(false);
                            },
                            ApproachTimeout,
                            TimeSpan.FromMilliseconds(150),
                            $"{chainName}::WaitInRange")
                        .ExecuteAsync(ctx);

                    pathfinder.Stop();
                    vnav.Stop();

                    if (result.IsCanceled)
                    {
                        return StepResult.Canceled();
                    }

                    if (objects.LocalPlayer is { } after
                        && IsReadyForLifestream(zone, lifestream, after.Position))
                    {
                        return StepResult.Success();
                    }

                    return StepResult.Failure("Could not approach within Lifestream range.");
                }), $"{chainName}::CloseIn");
    }

    public static bool IsReadyForLifestream(IZone zone, ILifestreamIpc lifestream, Vector3 position)
    {
        try
        {
            if (lifestream.GetActiveCustomAetheryte() != 0)
            {
                return true;
            }
        }
        catch
        {
            // IPC optional — fall through to distance check.
        }

        return zone.IsWithinLifestreamRange(position);
    }

    public static bool IsAlreadyAtAetheryte(AethernetData? aetheryte, Vector3 position)
    {
        if (aetheryte == null)
        {
            return false;
        }

        float pad = MathF.Max(aetheryte.DeadRadius, AethernetData.InteractRadius);
        return position.Distance2D(aetheryte.Position) <= pad
               || position.Distance2D(aetheryte.GetInteractPosition()) <= pad;
    }
}
