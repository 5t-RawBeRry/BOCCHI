using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace BOCCHI.Treasure.Hunt;

public readonly record struct TreasureLayoutDatum(uint Id, Vector3 Position, uint ModelId);

public class TreasureHuntPathfinder : HuntRoutePlanner
{
    private readonly List<TreasureLayoutDatum> treasure;

    public TreasureHuntPathfinder(
        ZoneId zoneId,
        IDalamudPluginInterface plugin,
        List<TreasureLayoutDatum> treasure,
        IPluginLog log
    ) : base(zoneId, plugin, log)
    {
        this.treasure = treasure;
        LoadFile("precomputed_treasure_hunt_data.json");
    }

    protected override Vector3 GetNodePosition(uint nodeId)
    {
        TreasureLayoutDatum match = treasure.First(t => t.Id == nodeId);
        return match.Position;
    }
}
