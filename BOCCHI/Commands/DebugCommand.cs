using BOCCHI.Common;
using BOCCHI.Common.Config;
using Dalamud.Plugin.Services;
using Ocelot.Rotation.Services.BossMod;
using Ocelot.Services.Commands;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;

namespace BOCCHI.Commands;

public class DebugCommand
(
    BossModRotationService bossModRotation,
    IPlayer player,
    IChatGui chat,
    UIConfig uiConfig,
    ITranslator<DebugCommand> translator
) : OcelotCommand(translator)
{
    public override string Command => "debug";

    public override List<string> Aliases => [];

    public override bool Hidden => true;

    public override void Execute(CommandContext context)
    {
        if (context.Args.Length == 0)
        {
            chat.PrintError("Usage: /bocchi debug ai-preset");
            return;
        }

        switch (context.Args[0].ToLowerInvariant())
        {
            case "ai-preset":
            case "make-ai-preset":
                MakeAiPreset();
                break;
            default:
                chat.PrintError("Unknown argument. Try: ai-preset");
                break;
        }
    }

    private void MakeAiPreset()
    {
        var job = player.GetClassJob();
        chat.Print(BocchiChat.Format(
            $"Base job={job?.Abbreviation.ToString() ?? "?"} Role={job?.Role.ToString() ?? "?"} "
            + $"IsMelee={player.IsMelee()} IsMeleeDps={player.IsMeleeDps()}",
            uiConfig));

        if (!bossModRotation.TryEnsureBocchiAiPreset(out string? storedJson))
        {
            chat.PrintError(BocchiChat.Format("Failed to create BOCCHI AI preset (is BossMod / BMR loaded?)", uiConfig));
            return;
        }

        chat.Print(BocchiChat.Format($"Created/updated preset '{BossModRotationService.AiPresetName}'.", uiConfig));
        if (string.IsNullOrWhiteSpace(storedJson))
        {
            chat.PrintError(BocchiChat.Format(
                "Preset Create succeeded but Get returned empty — check BossMod Presets IPC.",
                uiConfig));
            return;
        }

        chat.Print(BocchiChat.Format($"Stored JSON:\n{storedJson}", uiConfig));
    }
}
