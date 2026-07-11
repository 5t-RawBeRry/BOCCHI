using BOCCHI.Common.Data.Aethernet;

namespace BOCCHI.Common.Data.Zones;

public static class ZoneAethernetExtensions
{
    public static AethernetData? FindAetheryte(this IZone zone, uint placeNameId)
    {
        return zone.GetAetherytes()
            .Concat(zone.GetAethernetShards())
            .FirstOrDefault(aetheryte => aetheryte.Id == placeNameId);
    }
}
