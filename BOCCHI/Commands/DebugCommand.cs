using Dalamud.Plugin.Services;
using Ocelot.Rotation.Services.BossMod;
using Ocelot.Services.Commands;
using Ocelot.Services.Translation;

namespace BOCCHI.Commands;

public class DebugCommand
(
    BossModRotationService bossModRotation,
    IChatGui chat,
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
        if (!bossModRotation.TryEnsureBocchiAiPreset(out string? storedJson))
        {
            chat.PrintError("[BOCCHI] Failed to create BOCCHI AI preset (is BossMod / BMR loaded?)");
            return;
        }

        chat.Print($"[BOCCHI] Created/updated preset '{BossModRotationService.AiPresetName}'.");
        if (string.IsNullOrWhiteSpace(storedJson))
        {
            chat.PrintError("[BOCCHI] Preset Create succeeded but Get returned empty — check BossMod Presets IPC.");
            return;
        }

        chat.Print($"[BOCCHI] Stored JSON:\n{storedJson}");
    }
}
