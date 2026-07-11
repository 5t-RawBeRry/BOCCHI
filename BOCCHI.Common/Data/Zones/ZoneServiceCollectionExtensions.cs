using Microsoft.Extensions.DependencyInjection;

namespace BOCCHI.Common.Data.Zones;

public static class ZoneServiceCollectionExtensions
{
    public static IServiceCollection AddZones(this IServiceCollection services)
    {
        services.AddSingleton<IZoneProvider, ZoneProvider>();
        return services;
    }

    public static IServiceCollection AddZone<TZone>(this IServiceCollection services)
        where TZone : class, IZone
    {
        services.AddSingleton<TZone>();
        services.AddSingleton<IZone>(sp => sp.GetRequiredService<TZone>());
        return services;
    }
}
