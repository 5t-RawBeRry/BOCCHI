using Ocelot.Services.Commands;
using Ocelot.Services.Translation;

namespace BOCCHI.Commands;

/// <summary>Legacy OCH prefixes — same behavior as <c>/bocchi</c> (window + all subcommands).</summary>
public class OchAliasCommand(IMainCommand main, ITranslator<OchAliasCommand> translator) : OcelotCommand(translator)
{
    public override string Command => "och";

    public override List<string> Aliases => ["occultcrescenthelper"];

    /// <summary>Listed under /bocchi in the plugin description; keep the slash command itself quiet in Dalamud help.</summary>
    public override bool Hidden => true;

    public override void Execute(CommandContext context) => main.Execute(context);
}
