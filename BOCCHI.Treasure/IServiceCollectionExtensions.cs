using BOCCHI.Common;
using BOCCHI.Treasure.ChainRecipes;
using BOCCHI.Treasure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BOCCHI.Treasure;

public static class IServiceCollectionExtensions
{
    public static void LoadTreasureModule(this IServiceCollection services)
    {
        services.AddSingleton<ITreasureTracker, TreasureTracker>();
        services.AddSingleton<ICarrotTracker, CarrotTracker>();
        services.AddSingleton<ITreasureHunter, TreasureHunterService>();
        services.AddSingleton<CofferObservationSubmissionService>();
        services.AddSingleton<Func<ITreasureHunter>>(sp => () => sp.GetRequiredService<ITreasureHunter>());
        services.AddSingleton<IDynamicRenderer, TreasureRenderer>();
        services.AddSingleton<TreasureRadarDrawer>();
        services.AddSingleton<OpenTreasureCofferChain>();
        services.AddSingleton<HuntTeleportChain>();
        services.AddSingleton<HuntTreasureSightChain>();
    }
}
