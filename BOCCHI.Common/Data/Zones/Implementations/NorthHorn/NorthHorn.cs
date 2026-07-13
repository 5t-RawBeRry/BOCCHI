using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Zones.Graph.Factory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Implementations.NorthHorn;

public class NorthHorn(
    IObjectTable objects,
    IDalamudPluginInterface plugin,
    IGraphFactory graphs,
    IPathfinder pathfinder,
    ILogger logger
) : BaseZone(objects, plugin, graphs, pathfinder, logger, ZoneId.Unknown)
{
    protected override uint BasecampPlaceNameId => 0;

    public override AethernetData GetMainAetheryte()
    {
        throw new NotSupportedException("North Horn zone data is not configured yet.");
    }

    public override Vector3 GetAetherytePosition()
    {
        return Vector3.Zero;
    }

    public override Vector3 GetStartingPosition()
    {
        return Vector3.Zero;
    }

    protected override ushort GetForkedTowerEventId()
    {
        return 0;
    }
}
