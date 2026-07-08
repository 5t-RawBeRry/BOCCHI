using BOCCHI.Common;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Microsoft.Extensions.DependencyInjection;
using Ocelot;
using Ocelot.States;

namespace BOCCHI.MobFarmer;

public static class IServiceCollectionExtensions
{
    public static void LoadMobFarmerModule(this IServiceCollection services)
    {
        Registry.RegisterAssemblies(typeof(FarmerPhase).Assembly);

        services.AddSingleton<IMobScanner, MobScanner>();
        services.AddSingleton<IRotationPlugin, BlankRotationPlugin>();
        services.AddSingleton<IMobFarmer, MobFarmerService>();
        services.AddSingleton<IDynamicRenderer, MobFarmerRenderer>();
        services.AddSingleton<MobFarmerDebugDrawer>();

        services.AddFlowStateMachine<FarmerPhase>(FarmerPhase.Waiting);
    }
}
