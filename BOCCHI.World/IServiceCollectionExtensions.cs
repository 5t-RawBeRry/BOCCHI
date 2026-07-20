using BOCCHI.Common;
using BOCCHI.Common.Services;
using BOCCHI.CriticalEncounters;
using BOCCHI.CriticalEncounters.Data;
using BOCCHI.CriticalEncounters.Services;
using BOCCHI.Fates;
using BOCCHI.Fates.Data;
using BOCCHI.Fates.Services;
using Microsoft.Extensions.DependencyInjection;
namespace BOCCHI.World;

public static class IServiceCollectionExtensions
{
    public static void LoadWorldModule(this IServiceCollection services)
    {
        services.AddSingleton<IDynamicRenderer, FatesRenderer>();
        services.AddSingleton<IFateRepository, FateRepository>();
        services.AddSingleton<IFateFactory, FateFactory>();
        services.AddSingleton<IFateContext, FateContext>();
        services.AddSingleton<IFateScorer, FateScorer>();

        services.AddSingleton<IDynamicRenderer, CriticalEncountersRenderer>();
        services.AddSingleton<ICriticalEncounterRepository, CriticalEncounterRepository>();
        services.AddSingleton<ICriticalEncounterFactory, CriticalEncounterFactory>();
        services.AddSingleton<ICriticalEncounterContext, CriticalEncounterContext>();
    }
}
