using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones.Graph;
using System.Numerics;
namespace BOCCHI.Common.Data.Zones;

public interface IZone
{
    ZoneId ZoneId { get; }

    ushort TerritoryType { get; }

    ushort ForkedTowerEventId { get; }

    bool IsOccultCrescentZone();

    bool IsInBasecamp();

    AethernetData GetMainAetheryte();

    Vector3 GetAetherytePosition();

    Vector3 GetStartingPosition();

    List<AethernetData> GetAetherytes();

    List<AethernetData> GetAethernetShards();

    List<AethernetData> GetNearbyAethernetShards();

    bool HasNearbyAethernetShards() => GetNearbyKnowledgeCrystals().Count != 0;

    List<KnowledgeCrystalData> GetKnowledgeCrystals();

    List<KnowledgeCrystalData> GetNearbyKnowledgeCrystals();

    bool HasNearbyKnowledgeCrystals() => GetNearbyKnowledgeCrystals().Count != 0;

    bool IsInForkedTower();

    float GetCriticalEncounterRadius(int eventId);

    bool IsPotFate(int fateId)
    {
        return GetPotFateData().Any(f => f.Id == fateId);
    }

    List<ActivityData> GetNormalFateData() => [];

    List<ActivityData> GetPotFateData() => [];

    List<ActivityData> GetCriticalEncounterData() => [];

    List<TreasureData> GetTreasureData() => [];

    Dictionary<int, List<PotChestData>> GetPotChestData() => [];

    List<PotChestData> GetRerollPotChestData() => [];

    List<CarrotData> GetCarrotData() => [];

    Task<ZoneGraph> GetGraph();
}
