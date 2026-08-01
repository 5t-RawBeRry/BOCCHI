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
        float closestDistance = float.MaxValue;
        TreasureLayoutDatum startTreasure = treasure[0];

        foreach(TreasureLayoutDatum treasureData in treasure)
        {
            if (!nodes.Contains(treasureData.Id))
            {
                continue;
            }

            float distance = Vector3.Distance(start, treasureData.Position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                startTreasure = treasureData;
            }
        }

        return startTreasure.Id;
    }
}
