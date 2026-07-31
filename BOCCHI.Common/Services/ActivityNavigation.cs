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
    ILogger<ActivityNavigation> logger
) : IActivityNavigation
{
    private const string ChainPrefix = "ActivityGoto::";

    public bool CanPathfind => vnav.IsNavmeshReady();

    public bool CanTeleport(Vector3 destination, out string? disabledReason)
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            disabledReason = "Not in a supported Occult Crescent zone.";
            return false;
        }

        AethernetData? target = GetNearestAetheryte(destination);
        if (target == null)
        {
            disabledReason = "No aethernet destination found.";
            return false;
        }

        if (!zone.IsWithinLifestreamRange(player.Position))
        {
            disabledReason = "You must be near an aetheryte to teleport.";
            return false;
        }

        if (AetheryteApproach.IsAlreadyAtAetheryte(target, player.Position))
        {
            disabledReason = "You're already at this aetheryte.";
            return false;
        }

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
        AethernetData? target = GetNearestAetheryte(destination);
        if (target == null)
        {
            logger.Warning("No aethernet found for teleport toward {Name}", name);
            return;
        }

        if (!CanTeleport(destination, out string? reason))
        {
            logger.Warning("Cannot teleport toward {Name}: {Reason}", name, reason ?? "unknown");
            return;
        }

        Vector3 approach = NavigationApproach.GetEventPosition(destination, target.Position);
        logger.Info("Teleporting via {Aethernet} toward {Name}", target.Id, name);

        CancelActivityChains();

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
    }

    private IChain BuildPathChain(string name, Vector3 destination) =>
        AppendPath(chains.Create(name), name, destination);

    private IChain AppendPath(IChain chain, string name, Vector3 destination)
    {
        return chain
            .Then(_ =>
            {
                if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
                {
                    return StepResult.Success();
                }

                if (objects.LocalPlayer is not { } localPlayer)
                {
                    return StepResult.Success();
                }

                if (localPlayer.Position.Distance(destination) > 50f)
                {
                    Actions.MountRoulette.Cast();
                }

                return StepResult.Success();
            }, $"{name}::MaybeMount")
            .Then<PathfindToChain, PathfinderConfig>(new(destination)
            {
                DistanceThreshold = 2f,
                ShouldSnapToFloor = true
            });
    }

    private AethernetData? GetNearestAetheryte(Vector3 destination)
    {
        return zones.GetZone().GetAetherytes()
            .OrderBy(a => destination.Distance2D(a.Position))
            .FirstOrDefault();
    }

    private void CancelActivityChains() => manager.CancelWhere(name => name.StartsWith(ChainPrefix, StringComparison.Ordinal));
}
