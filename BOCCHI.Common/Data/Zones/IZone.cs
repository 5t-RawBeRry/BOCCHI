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

    List<KnowledgeCrystalData> GetNearbyKnowledgeCrystals();

    bool HasNearbyKnowledgeCrystals() => GetNearbyKnowledgeCrystals().Count != 0;

    /// <summary>
    ///     True when standing where crystal buffs can be cast: inside the authored buff radius
    ///     (including on the crystal), or ≤5y of a nearby knowledge crystal / shard crystal.
    /// </summary>
    bool IsInBuffCastRange(Vector3 position)
    {
        if (GetBuffZone() is { } buffZone && buffZone.IsWithinCastRadius2D(position))
        {
            return true;
        }

        const float crystalInteractionRange = 5f;
        float maxSq = crystalInteractionRange * crystalInteractionRange;
        foreach (KnowledgeCrystalData crystal in GetNearbyKnowledgeCrystals())
        {
            float dx = position.X - crystal.Position.X;
            float dz = position.Z - crystal.Position.Z;
            if ((dx * dx) + (dz * dz) <= maxSq)
            {
                return true;
            }
        }

        return false;
    }

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

    BuffZone? GetBuffZone() => null;

    TreasureRoutePolicy GetTreasureRoutePolicy() => new();

    ShoppingVendorData? GetShoppingVendor() => null;

    Task<ZoneGraph> GetGraph();
}
