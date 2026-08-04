using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace BOCCHI.Treasure.Hunt;

public readonly record struct TreasureLayoutDatum(uint Id, Vector3 Position, uint ModelId);

public class TreasureHuntPathfinder : HuntRoutePlanner
{
    private readonly List<TreasureLayoutDatum> treasure;

    private readonly Vector3 routeSeed;

    public TreasureHuntPathfinder(
        ZoneId zoneId,
        IDalamudPluginInterface plugin,
        List<TreasureLayoutDatum> treasure,
        Vector3 routeSeed,
        IPluginLog log,
        float teleportCost
    ) : base(zoneId, plugin, log, teleportCost)
    {
        this.treasure = treasure;
        this.routeSeed = routeSeed;
        LoadFile("precomputed_treasure_hunt_data.json");
    }

    protected override Vector3 GetRouteSeedPosition() => routeSeed;

    protected override Vector3 GetNodePosition(uint nodeId)
    {
        TreasureLayoutDatum match = treasure.First(t => t.Id == nodeId);
        return match.Position;
    }

    protected override IReadOnlyList<uint> GetAllRouteNodes() =>
        treasure.Select(t => t.Id).ToList();
}
