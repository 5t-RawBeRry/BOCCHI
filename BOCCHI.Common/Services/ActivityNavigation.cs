using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Recipes;
using Ocelot.Extensions;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using System.Numerics;
using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Services;

public class ActivityNavigation
(
    IChainFactory chains,
    IChainManager manager,
    IZoneProvider zones,
    IObjectTable objects,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    ILifestreamIpc lifestream,
    ICondition conditions,
    IPlayer player,
    IFramework framework,
    AutomatorConfig automatorConfig,
    ILogger<ActivityNavigation> logger
) : IActivityNavigation
{
    private const string ChainPrefix = "ActivityGoto::";

    private int teleportGeneration;

    public bool CanPathfind => vnav.IsNavmeshReady();

    public bool CanTeleport(Vector3 destination, out string? disabledReason)
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            disabledReason = "Not in a supported Occult Crescent zone.";
            return false;
        }

        if (zone.GetAetherytes().Count == 0)
        {
            disabledReason = "No aethernet destination found.";
            return false;
        }

        if (!zone.IsWithinLifestreamRange(player.Position))
        {
            disabledReason = "You must be near an aetheryte to teleport.";
            return false;
        }

        // Do not gate on "already at nearest" — Euclidean nearest can be an island
        // shard that cannot walk to the destination (e.g. Unhallowed Hamlet → Eye to Eye).
        disabledReason = null;
        return true;
    }

    public void PathTo(Vector3 destination, string name, string id)
    {
        if (!CanPathfind)
        {
            logger.Warning("Navmesh not ready — cannot path to {Name}", name);
            return;
        }

        Vector3 approach = NavigationApproach.GetEventPosition(destination, player.Position);
        logger.Info("Pathfinding to {Name} at {Destination:f1}", name, approach);

        CancelActivityChains();
        _ = manager.Manage(BuildPathChain($"{ChainPrefix}Path::{id}", approach));
    }

    public void TeleportToward(Vector3 destination, string name, string id)
    {
        if (!CanTeleport(destination, out string? reason))
        {
            logger.Warning("Cannot teleport toward {Name}: {Reason}", name, reason ?? "unknown");
            return;
        }

        int generation = Interlocked.Increment(ref teleportGeneration);
        manager.CancelWhere(name => name.StartsWith(ChainPrefix, StringComparison.Ordinal));
        _ = TeleportTowardAsync(destination, name, id, generation);
    }

    private async Task TeleportTowardAsync(Vector3 destination, string name, string id, int generation)
    {
        try
        {
            AethernetData? target = await SelectBestAetheryteAsync(destination).ConfigureAwait(false);
            if (generation != teleportGeneration)
            {
                return;
            }

            await framework.Run(() =>
            {
                if (generation != teleportGeneration)
                {
                    return;
                }

                if (target == null)
                {
                    logger.Warning("No aethernet found for teleport toward {Name}", name);
                    return;
                }

                if (AetheryteApproach.IsAlreadyAtAetheryte(target, player.Position))
                {
                    logger.Info("Already at best aethernet {Aethernet} for {Name} — pathfinding", target.Id, name);
                    PathTo(destination, name, id);
                    return;
                }

                Vector3 approach = NavigationApproach.GetEventPosition(destination, target.Position);
                logger.Info("Teleporting via {Aethernet} toward {Name}", target.Id, name);

                IChain chain = AethernetTeleport.BuildChain(
                    chains.Create($"{ChainPrefix}Teleport::{id}"),
                    chains,
                    zones,
                    objects,
                    pathfinder,
                    vnav,
                    lifestream,
                    logger,
                    target.Id,
                    automatorConfig.SprintOnAetheryteApproach);

                if (CanPathfind)
                {
                    chain = AppendPath(chain, $"{ChainPrefix}Teleport::{id}", approach);
                }

                _ = manager.Manage(chain);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed teleport toward {Name}", name);
        }
    }

    private IChain BuildPathChain(string name, Vector3 destination) =>
        AppendPath(chains.Create(name), name, destination);

    private IChain AppendPath(IChain chain, string name, Vector3 destination)
    {
        DateTime mountStarted = DateTime.MinValue;

        return chain
            .Then(_ =>
            {
                mountStarted = DateTime.UtcNow;

                if (MountWait.ShouldSkip(conditions, objects, destination, automatorConfig.ShouldAutoMount))
                {
                    return StepResult.Success();
                }

                MountWait.TryCast(automatorConfig.PreferredMountId);
                return StepResult.Success();
            }, $"{name}::MaybeMount")
            .WaitUntil(
                _ =>
                {
                    DateTime started = mountStarted == DateTime.MinValue ? DateTime.UtcNow : mountStarted;
                    return ValueTask.FromResult(
                        MountWait.IsReadyOrGiveUp(
                            conditions,
                            objects,
                            destination,
                            started,
                            automatorConfig.ShouldAutoMount,
                            automatorConfig.PreferredMountId));
                },
                MountWait.Timeout,
                TimeSpan.FromMilliseconds(250),
                $"{name}::WaitForMount")
            .Then<PathfindToChain, PathfinderConfig>(new(destination)
            {
                DistanceThreshold = 2f,
                ShouldSnapToFloor = true
            });
    }

    /// <summary>
    ///     Pick an aethernet that can walk to <paramref name="destination"/>.
    ///     Tries Euclidean-nearest first and returns the first reachable shard so the UI
    ///     button starts Lifestream after typically one vnav query (scoring every shard
    ///     felt sluggish). Unreachable nearest (island water gaps) are skipped.
    /// </summary>
    private async Task<AethernetData?> SelectBestAetheryteAsync(Vector3 destination)
    {
        List<AethernetData> aetherytes = zones.GetZone().GetAetherytes();
        if (aetherytes.Count == 0)
        {
            return null;
        }

        AethernetData? ByEuclidean() => aetherytes
            .OrderBy(a => destination.Distance2D(a.Position))
            .FirstOrDefault();

        if (!vnav.IsNavmeshReady())
        {
            return ByEuclidean();
        }

        foreach (AethernetData aetheryte in aetherytes.OrderBy(a => destination.Distance2D(a.Position)))
        {
            Vector3 from = aetheryte.GetInteractPosition();
            Path path = await pathfinder.Pathfind(new PathfinderConfig(destination)
                {
                    From = from,
                    AllowFlying = false,
                    ShouldSnapToFloor = true
                })
                .ConfigureAwait(false);

            if (path.Nodes.Count >= 2)
            {
                return aetheryte;
            }
        }

        return ByEuclidean();
    }

    private void CancelActivityChains()
    {
        Interlocked.Increment(ref teleportGeneration);
        manager.CancelWhere(name => name.StartsWith(ChainPrefix, StringComparison.Ordinal));
    }
}
