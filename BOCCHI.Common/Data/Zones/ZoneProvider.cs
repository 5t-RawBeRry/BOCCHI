using Dalamud.Plugin.Services;

namespace BOCCHI.Common.Data.Zones;

public class ZoneProvider(IClientState client, IEnumerable<IZone> zones) : IZoneProvider
{
    private readonly Dictionary<ushort, IZone> zoneMap = zones.ToDictionary(z => z.TerritoryType);

    public IZone GetZone() => zoneMap.TryGetValue((ushort)client.TerritoryType, out IZone? zone) ? zone : new NullZone();
}
