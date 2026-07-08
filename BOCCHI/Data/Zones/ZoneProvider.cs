using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BOCCHI.Data.Zones;

public class ZoneProvider(IClientState client, IServiceProvider services) : IZoneProvider
{
    private readonly Dictionary<ushort, IZone> Zones = new()
    {
        { 1252, services.GetRequiredService<SouthHorn>() },
    };


    public IZone GetZone()
    {
        return Zones.TryGetValue((ushort)client.TerritoryType, out var zone) ? zone : new NullZone();
    }
}
