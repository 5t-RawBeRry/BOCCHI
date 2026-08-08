using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
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

    private int navigationGeneration;

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

        // Near an aetheryte: hop best shard then walk. Otherwise walk from here.
        if (CanTeleport(destination, out _))
        {
            int generation = BeginNavigation();
            _ = PathViaAethernetAsync(destination, name, id, generation);
            return;
        }

        if (!TryBuildApproach(destination, player.Position, out Vector3 approach, out bool alreadyAtCeRing))
        {
            return;
        }

        if (alreadyAtCeRing)
        {
            logger.Info("Already inside CE wait area for {Name} — not pathing into the center", name);
            CancelActivityChains();
            pathfinder.Stop();
            return;
        }

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

        int generation = BeginNavigation();
        _ = TeleportOnlyAsync(destination, name, id, generation);
    }

    private async Task PathViaAethernetAsync(Vector3 destination, string name, string id, int generation)
    {
        try
        {
            AethernetData? target = await SelectBestAetheryteAsync(destination).ConfigureAwait(false);
            if (generation != navigationGeneration)
            {
                return;
            }

            await framework.Run(() =>
            {
                if (generation != navigationGeneration)
                {
                    return;
                }

                Vector3 approachFrom = target?.Position ?? player.Position;
                if (!TryBuildApproach(destination, approachFrom, out Vector3 approach, out bool alreadyAtCeRing))
                {
                    return;
                }

                if (alreadyAtCeRing)
                {
                    logger.Info("Already inside CE wait area for {Name} — not pathing into the center", name);
                    pathfinder.Stop();
                    return;
                }

                if (target == null || AetheryteApproach.IsAlreadyAtAetheryte(target, player.Position))
                {
                    logger.Info("Pathfinding to {Name} at {Destination:f1}", name, approach);
                    _ = manager.Manage(BuildPathChain($"{ChainPrefix}Path::{id}", approach));
                    return;
                }

                logger.Info(
                    "Path via aethernet {Aethernet} then walk to {Name}",
                    target.Id,
                    name);

                IChain chain = AethernetTeleport.BuildChain(
                    chains.Create($"{ChainPrefix}Path::{id}"),
                    chains,
                    zones,
                    objects,
                    pathfinder,
                    vnav,
                    lifestream,
                    logger,
                    target.Id,
                    automatorConfig.SprintOnAetheryteApproach);

                chain = AppendPath(chain, $"{ChainPrefix}Path::{id}", approach);
                _ = manager.Manage(chain);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed path via aethernet toward {Name}", name);
        }
    }

    private async Task TeleportOnlyAsync(Vector3 destination, string name, string id, int generation)
    {
        try
        {
            AethernetData? target = await SelectBestAetheryteAsync(destination).ConfigureAwait(false);
            if (generation != navigationGeneration)
            {
                return;
            }

            await framework.Run(() =>
            {
                if (generation != navigationGeneration)
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
                    logger.Info(
                        "Already at best aethernet {Aethernet} for {Name} — teleport does not pathfind",
                        target.Id,
                        name);
                    return;
                }

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

    private bool TryBuildApproach(
        Vector3 destination,
        Vector3 from,
        out Vector3 approach,
        out bool alreadyAtCeRing)
    {
        alreadyAtCeRing = false;
        IZone zone = zones.GetZone();
        if (NavigationApproach.TryResolveCriticalEncounterApproach(
                zone, destination, from, out approach, out ActivityData? activity)
            && activity?.CombatRadius is { } radius)
        {
            alreadyAtCeRing = NavigationConstants.IsInsideCriticalEncounterWaitArea(
                destination, radius, activity.AreaShape, from);
            return true;
        }

        approach = NavigationApproach.GetEventPosition(destination, from);
        return true;
    }

    private IChain AppendPath(IChain chain, string name, Vector3 destination) =>
        chain.Then<PathfindToChain, PathfinderConfig>(new(destination)
        {
            DistanceThreshold = 2f,
            ShouldSnapToFloor = true,
            WhileMoving = () => MountWait.TryCastIfNeeded(
                conditions,
                objects,
                destination,
                automatorConfig.ShouldAutoMount,
                automatorConfig.PreferredMountId,
                zones.GetZone().IsInBasecamp()),
        });

    /// <summary>
    ///     Pick an aethernet that can walk to <paramref name="destination"/>.
    ///     Honors authored preferred shards for known activities (Eye to Eye → Crown, not Unhallowed),
    ///     then scores a few Euclidean-near reachable shards by walk distance so island gaps do not win.
    /// </summary>
    private async Task<AethernetData?> SelectBestAetheryteAsync(Vector3 destination)
    {
        List<AethernetData> aetherytes = zones.GetZone().GetAetherytes();
        if (aetherytes.Count == 0)
        {
            return null;
        }

        uint? preferredId = FindPreferredAethernetId(destination);

        AethernetData? ByEuclidean() => aetherytes
            .OrderBy(a => preferredId is { } pid && a.Id == pid ? 0 : 1)
            .ThenBy(a => destination.Distance2D(a.Position))
            .FirstOrDefault();

        if (!vnav.IsNavmeshReady())
        {
            return ByEuclidean();
        }

        // Preferred first, then a few Euclidean-nearest. Cap queries so the UI button stays snappy.
        List<AethernetData> candidates = [];
        if (preferredId is { } preferred)
        {
            AethernetData? preferredShard = aetherytes.FirstOrDefault(a => a.Id == preferred);
            if (preferredShard != null)
            {
                candidates.Add(preferredShard);
            }
        }

        foreach (AethernetData aetheryte in aetherytes.OrderBy(a => destination.Distance2D(a.Position)))
        {
            if (candidates.Count >= 4)
            {
                break;
            }

            if (candidates.Any(c => c.Id == aetheryte.Id))
            {
                continue;
            }

            candidates.Add(aetheryte);
        }

        AethernetData? best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (AethernetData aetheryte in candidates)
        {
            Vector3 from = aetheryte.GetInteractPosition();
            Path path = await pathfinder.Pathfind(new PathfinderConfig(destination)
                {
                    From = from,
                    AllowFlying = false,
                    ShouldSnapToFloor = true
                })
                .ConfigureAwait(false);

            // Unreachable paths report Distance 0 / fewer than 2 nodes.
            if (path.Nodes.Count < 2 || float.IsPositiveInfinity(path.Distance) || path.Distance <= 0f)
            {
                continue;
            }

            if (preferredId is { } pid && aetheryte.Id == pid)
            {
                return aetheryte;
            }

            if (path.Distance < bestDistance)
            {
                bestDistance = path.Distance;
                best = aetheryte;
            }
        }

        return best ?? ByEuclidean();
    }

    private uint? FindPreferredAethernetId(Vector3 destination)
    {
        IZone zone = zones.GetZone();
        const float matchRadius = 80f;
        foreach (ActivityData activity in zone.GetNormalFateData()
                     .Concat(zone.GetPotFateData())
                     .Concat(zone.GetCriticalEncounterData()))
        {
            if (activity.PreferredAethernetId is not { } preferred)
            {
                continue;
            }

            if (destination.Distance2D(activity.Position) <= matchRadius)
            {
                return preferred;
            }
        }

        return null;
    }

    private int BeginNavigation()
    {
        int generation = Interlocked.Increment(ref navigationGeneration);
        manager.CancelWhere(name => name.StartsWith(ChainPrefix, StringComparison.Ordinal));
        return generation;
    }

    private void CancelActivityChains()
    {
        Interlocked.Increment(ref navigationGeneration);
        manager.CancelWhere(name => name.StartsWith(ChainPrefix, StringComparison.Ordinal));
    }
}
