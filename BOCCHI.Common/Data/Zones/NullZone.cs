using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones.Graph;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

public class NullZone : IZone
{
    public ZoneId ZoneId => ZoneId.Unknown;

    public ushort TerritoryType => 0;

    public bool IsOccultCrescentZone() => false;

    public bool IsInBasecamp() => false;

    public AethernetData GetMainAetheryte() => new();

    public Vector3 GetAetherytePosition() => Vector3.NaN;

    public Vector3 GetStartingPosition() => Vector3.NaN;

    public List<AethernetData> GetAetherytes() => [];

    public List<AethernetData> GetAethernetShards() => [];

    public List<AethernetData> GetNearbyAethernetShards() => [];

    public List<KnowledgeCrystalData> GetKnowledgeCrystals() => [];

    public List<KnowledgeCrystalData> GetNearbyKnowledgeCrystals() => [];

    public ushort ForkedTowerEventId => 0;

    public bool IsInForkedTower() => false;

    public float GetCriticalEncounterRadius(int eventId) => 0f;

    public async Task<ZoneGraph> GetGraph() => new();
}
