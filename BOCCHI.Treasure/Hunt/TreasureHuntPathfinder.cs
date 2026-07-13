using System.Numerics;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace BOCCHI.Treasure.Hunt;

public readonly record struct TreasureLayoutDatum(uint Id, Vector3 Position, uint ModelId);

public class TreasureHuntPathfinder : HuntRoutePlanner
{
    private readonly List<TreasureLayoutDatum> treasure;

    public TreasureHuntPathfinder(
        ZoneId zoneId,
        IDalamudPluginInterface plugin,
        List<TreasureLayoutDatum> treasure,
        IPluginLog log,
        float returnCost,
        float teleportCost
    ) : base(zoneId, plugin, log, returnCost, teleportCost)
    {
        this.treasure = treasure;
        LoadFile("precomputed_treasure_hunt_data.json");
    }

    protected override uint GetStartingNode(Vector3 start, List<uint> nodes)
    {
        var closestDistance = float.MaxValue;
        var startTreasure = treasure[0];

        foreach (var treasureData in treasure)
        {
            if (!nodes.Contains(treasureData.Id))
            {
                continue;
            }

            var distance = Vector3.Distance(start, treasureData.Position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                startTreasure = treasureData;
            }
        }

        return startTreasure.Id;
    }
}
