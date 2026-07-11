using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BOCCHI.Common.Data.Zones;

public class ZoneProvider(IClientState client, IEnumerable<IZone> zones) : IZoneProvider
{
    private readonly Dictionary<ushort, IZone> zoneMap = zones.ToDictionary(z => z.TerritoryType);

    public IZone GetZone()
    {
        return zoneMap.TryGetValue((ushort)client.TerritoryType, out var zone) ? zone : new NullZone();
    }
}
