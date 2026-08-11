using BOCCHI.Services.Changelog;
using Ocelot.Services.Commands;
using Ocelot.Services.Translation;

namespace BOCCHI.Commands;

public class ChangelogCommand
(
    IChangelogWindow changelogWindow,
    ITranslator<ChangelogCommand> translator
) : OcelotCommand(translator)
{
    public override string Command => "changelog";

    public override List<string> Aliases => ["whatsnew", "whats-new"];

    public override void Execute(CommandContext context)
    {
        changelogWindow.ShowForCurrentVersion();
    }
}
