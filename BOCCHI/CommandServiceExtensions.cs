using BOCCHI.Commands;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Services.Commands;

namespace BOCCHI;

public static class CommandServiceExtensions
{
    public static void AddBocchiCommands(this IServiceCollection services)
    {
        services.AddSingleton<BuffCommand>();
        services.AddSingleton<IOcelotCommand>(sp => sp.GetRequiredService<BuffCommand>());
        services.AddSingleton<IMainCommandDelegate, BuffCommandDelegate>();

        services.AddSingleton<TeleportCommand>();
        services.AddSingleton<IOcelotCommand>(sp => sp.GetRequiredService<TeleportCommand>());
        services.AddSingleton<IMainCommandDelegate, TeleportCommandDelegate>();

        services.AddSingleton<IllegalCommand>();
        services.AddSingleton<IOcelotCommand>(sp => sp.GetRequiredService<IllegalCommand>());
        services.AddSingleton<IMainCommandDelegate, IllegalCommandDelegate>();

        services.AddSingleton<CmdCommand>();
        services.AddSingleton<IOcelotCommand>(sp => sp.GetRequiredService<CmdCommand>());
        services.AddSingleton<IMainCommandDelegate, CmdCommandDelegate>();

        services.AddSingleton<IOcelotCommand, OchAliasCommand>();
    }
}
