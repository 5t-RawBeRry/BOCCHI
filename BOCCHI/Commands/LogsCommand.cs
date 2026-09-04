using BOCCHI.Services.Logging;
using BOCCHI.Common.Services.Logging;
using Ocelot.Services.Commands;
using Ocelot.Services.Translation;

namespace BOCCHI.Commands;

public class LogsCommand
(
    ILogsWindow logWindow,
    IBocchiLogClipboard clipboard,
    ITranslator<LogsCommand> translator
) : OcelotCommand(translator)
{
    public override string Command => "logs";

    public override List<string> Aliases => ["log"];

    public override void Execute(CommandContext context)
    {
        if (context.Args.Length > 0
            && context.Args[0].Equals("copy", StringComparison.OrdinalIgnoreCase))
        {
            clipboard.CopyAll(announceInChat: true);
            return;
        }

        logWindow.Toggle();
    }
}
