using BOCCHI.Common;
using BOCCHI.Currency;
using BOCCHI.Currency.Services;
using BOCCHI.Experience;
using BOCCHI.Experience.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BOCCHI.Trackers;

public static class IServiceCollectionExtensions
{
    public static void LoadTrackersModule(this IServiceCollection services)
    {
        services.AddSingleton<IExperienceTracker, ExperienceTracker>();
        services.AddSingleton<IDynamicRenderer, ExperienceRenderer>();
        services.AddSingleton<ICurrencyTracker, CurrencyTracker>();
        services.AddSingleton<IDynamicRenderer, CurrencyRenderer>();
    }
}
