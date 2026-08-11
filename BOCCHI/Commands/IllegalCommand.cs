using BOCCHI.Automator.Services;
using Dalamud.Plugin.Services;
using Ocelot.Services.Commands;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.Commands;

public class IllegalCommand
(
    IAutomator automator,
    IMainWindow window,
    IChatGui chat,
    ITranslator<IllegalCommand> translator
) : OcelotCommand(translator)
{
    public override string Command => "illegal";

    public override List<string> Aliases => [];

    public override void Execute(CommandContext context)
    {
        if (context.Args.Length == 0)
        {
            window.Toggle();
            return;
        }

        switch (context.Args[0].ToLowerInvariant())
        {
            case "on":
                if (!automator.Enabled)
                {
                    automator.Toggle();
                }

                break;
            case "off":
                if (automator.Enabled)
                {
                    automator.Toggle();
                }

                break;
            case "toggle":
                automator.Toggle();
                break;
            default:
                chat.PrintError("Usage: /bocchi illegal [on|off|toggle]");
                break;
        }
    }
}
