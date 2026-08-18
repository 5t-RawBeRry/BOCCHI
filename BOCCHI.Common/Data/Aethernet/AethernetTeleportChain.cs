using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Aethernet;

/// <summary>Shared Lifestream aethernet hop used by Illegal Mode and Treasure Hunt.</summary>
public class AethernetTeleportChain
(
    IChainFactory chains,
    ILifestreamIpc lifestream,
    IZoneProvider zones,
    IObjectTable objects,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    AutomatorConfig config,
    MovementConfig movementConfig,
    ILogger<AethernetTeleportChain> logger
) : ChainRecipe<uint>(chains)
{
    public override string Name => "Aethernet Teleport";

    protected override IChain Compose(IChain chain, uint placeNameId) =>
        AethernetTeleport.BuildChain(
            chain, Chains, zones, objects, pathfinder, vnav, lifestream, logger, placeNameId,
            movementConfig.SprintOnAetheryteApproach);
}
