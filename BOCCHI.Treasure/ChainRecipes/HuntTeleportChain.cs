using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
namespace BOCCHI.Treasure.ChainRecipes;

public class HuntTeleportChain
(
    IChainFactory chains,
    ILifestreamIpc lifestream,
    IZoneProvider zones,
    IObjectTable objects,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    ILogger<HuntTeleportChain> logger
) : ChainRecipe<uint>(chains)
{
    public override string Name { get; } = "Hunt Teleport Chain";

    protected override IChain Compose(IChain chain, uint placeNameId) =>
        AethernetTeleport.BuildChain(chain, Chains, zones, objects, pathfinder, vnav, lifestream, logger, placeNameId);
}
