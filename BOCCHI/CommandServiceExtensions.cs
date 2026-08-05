using BOCCHI.Commands;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Services.Commands;

namespace BOCCHI;

public static class CommandServiceExtensions
{
    public static void AddBocchiCommands(this IServiceCollection services)
    {
        // Concrete commands are only reached via /bocchi <subcommand> (IMainCommandDelegate).
        services.AddSingleton<BuffCommand>();
        services.AddSingleton<IMainCommandDelegate, BuffCommandDelegate>();

        services.AddSingleton<TeleportCommand>();
        services.AddSingleton<IMainCommandDelegate, TeleportCommandDelegate>();

        services.AddSingleton<IllegalCommand>();
        services.AddSingleton<IMainCommandDelegate, IllegalCommandDelegate>();

        services.AddSingleton<CmdCommand>();
        services.AddSingleton<IMainCommandDelegate, CmdCommandDelegate>();

        services.AddSingleton<DebugCommand>();
        services.AddSingleton<IMainCommandDelegate, DebugCommandDelegate>();

        services.AddSingleton<IOcelotCommand, OchAliasCommand>();
    }
}
