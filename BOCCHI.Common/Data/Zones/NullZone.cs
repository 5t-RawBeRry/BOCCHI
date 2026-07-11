using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones.Graph;

namespace BOCCHI.Common.Data.Zones;

public class NullZone : IZone
{
    public ZoneId ZoneId => ZoneId.Unknown;

    public ushort TerritoryType => 0;

    public bool IsOccultCrescentZone()
    {
        return false;
    }

    public bool IsInBasecamp()
    {
        return false;
    }

    public AethernetData GetMainAetheryte()
    {
        return new AethernetData();
    }

    public Vector3 GetAetherytePosition()
    {
        return Vector3.NaN;
    }

    public Vector3 GetStartingPosition()
    {
        return Vector3.NaN;
    }

    public List<AethernetData> GetAetherytes()
    {
        return [];
    }

    public List<AethernetData> GetAethernetShards()
    {
        return [];
    }

    public List<AethernetData> GetNearbyAethernetShards()
    {
        return [];
    }

    public List<KnowledgeCrystalData> GetKnowledgeCrystals()
    {
        return [];
    }

    public List<KnowledgeCrystalData> GetNearbyKnowledgeCrystals()
    {
        return [];
    }

    public ushort ForkedTowerEventId => 0;

    public bool IsInForkedTower()
    {
        return false;
    }

    public float GetCriticalEncounterRadius(int eventId)
    {
        return 0f;
    }

    public async Task<ZoneGraph> GetGraph()
    {
        return new ZoneGraph();
    }
}
