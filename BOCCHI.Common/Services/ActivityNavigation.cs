using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
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
                    target.Id);

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

    private static readonly TimeSpan MountTimeout = TimeSpan.FromSeconds(12);

    private IChain BuildPathChain(string name, Vector3 destination) =>
        AppendPath(chains.Create(name), name, destination);

    private IChain AppendPath(IChain chain, string name, Vector3 destination)
    {
        return chain
            .Then(_ =>
            {
                if (ShouldSkipMount(destination))
                {
                    return StepResult.Success();
                }

                if (Actions.MountRoulette.CanCast())
                {
                    Actions.MountRoulette.Cast();
                }

                return StepResult.Success();
            }, $"{name}::MaybeMount")
            .WaitUntil(
                _ => ValueTask.FromResult(IsMountedOrShouldGiveUp(destination)),
                MountTimeout,
                TimeSpan.FromMilliseconds(250),
                $"{name}::WaitForMount")
            .Then<PathfindToChain, PathfinderConfig>(new(destination)
            {
                DistanceThreshold = 2f,
                ShouldSnapToFloor = true
            });
    }

    private bool ShouldSkipMount(Vector3 destination)
    {
        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            return true;
        }

        if (conditions[ConditionFlag.InCombat] || conditions[ConditionFlag.Unconscious])
        {
            return true;
        }

        if (objects.LocalPlayer is not { } localPlayer)
        {
            return true;
        }

        return localPlayer.Position.Distance(destination) <= NavigationConstants.MountMinDistance;
    }

    private bool IsMountedOrShouldGiveUp(Vector3 destination)
    {
        if (conditions[ConditionFlag.Mounted])
        {
            return true;
        }

        if (ShouldSkipMount(destination))
        {
            return true;
        }

        if (conditions[ConditionFlag.Mounting])
        {
            return false;
        }

        if (Actions.MountRoulette.CanCast())
        {
            Actions.MountRoulette.Cast();
        }

        return false;
    }

    /// <summary>
    ///     Pick the aethernet with the shortest walk to <paramref name="destination"/>.
    ///     Euclidean nearest is wrong across water (e.g. Unhallowed Hamlet island vs mainland CEs).
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

        (AethernetData Aetheryte, float Cost)[] scored = await Task.WhenAll(aetherytes.Select(async aetheryte =>
        {
            Vector3 from = aetheryte.GetInteractPosition();
            Path path = await pathfinder.Pathfind(new PathfinderConfig(destination)
                {
                    From = from,
                    AllowFlying = false,
                    ShouldSnapToFloor = true
                })
                .ConfigureAwait(false);

            float cost = path.Nodes.Count < 2 ? float.PositiveInfinity : path.Distance;
            return (aetheryte, cost);
        })).ConfigureAwait(false);

        (AethernetData Aetheryte, float Cost) best = scored
            .Where(s => !float.IsPositiveInfinity(s.Cost))
            .OrderBy(s => s.Cost)
            .FirstOrDefault();

        return best.Aetheryte ?? ByEuclidean();
    }

    private void CancelActivityChains()
    {
        Interlocked.Increment(ref teleportGeneration);
        manager.CancelWhere(name => name.StartsWith(ChainPrefix, StringComparison.Ordinal));
    }
}
