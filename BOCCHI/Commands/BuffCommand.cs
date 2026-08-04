using BOCCHI.Buff.Services;
using Dalamud.Plugin.Services;
using Ocelot.Services.Commands;
using Ocelot.Services.Translation;

namespace BOCCHI.Commands;

public class BuffCommand(IBuffRunner buffs, IChatGui chat, ITranslator<BuffCommand> translator) : OcelotCommand(translator)
{
    public override string Command => "buff";

    public override List<string> Aliases => [];

    public override void Execute(CommandContext context)
    {
        if (buffs.IsRunning)
        {
            buffs.Stop();
            chat.Print("Stopped buff run.");
            return;
        }

        if (!buffs.CanStart)
        {
            chat.PrintError(buffs.DisabledReason ?? "Cannot start buff run.");
            return;
        }

        buffs.Start();
        chat.Print("Queued buff run.");
    }
}
