using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Automator.ChainRecipes;

public class TeleportToAethernetChain
(
    IChainFactory chains,
    ILifestreamIpc lifestream,
    IZoneProvider zones,
    IObjectTable objects,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    AutomatorConfig config,
    ILogger<TeleportToAethernetChain> logger
) : ChainRecipe<uint>(chains)
{
    public override string Name
    {
        get => "Teleport to Aethernet Chain";
    }

    protected override IChain Compose(IChain chain, uint id) =>
        AethernetTeleport.BuildChain(
            chain, Chains, zones, objects, pathfinder, vnav, lifestream, logger, id,
            config.SprintOnAetheryteApproach);
}
