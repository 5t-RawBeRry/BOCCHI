using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common;
using Microsoft.Extensions.DependencyInjection;
using Ocelot;
using Ocelot.States;

namespace BOCCHI.Buff;

public static class IServiceCollectionExtensions
{
    public static void LoadBuffModule(this IServiceCollection services)
    {
        Registry.RegisterAssemblies(typeof(BuffState).Assembly);

        services.AddSingleton<IBuffProvider, BuffProvider>();
        services.AddSingleton<IBuffRunner, BuffRunner>();
        services.AddSingleton<IDynamicRenderer, BuffRenderer>();
        services.AddFlowStateMachine(BuffState.ApproachingKnowledgeCrystal, ServiceLifetime.Transient);
    }
}
