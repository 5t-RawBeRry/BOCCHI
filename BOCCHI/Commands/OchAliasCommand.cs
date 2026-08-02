using Ocelot.Services.Commands;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.Commands;

public class OchAliasCommand(IMainWindow window, ITranslator<OchAliasCommand> translator) : OcelotCommand(translator)
{
    public override string Command => "och";

    public override List<string> Aliases => ["occultcrescenthelper"];

    public override void Execute(CommandContext context) => window.Toggle();
}
