using System.Globalization;
using System.Numerics;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Debug;
using Dalamud.Plugin.Services;
using Ocelot.Rotation.Services;
using Ocelot.Rotation.Services.BossMod;
using Ocelot.Services.Commands;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;

namespace BOCCHI.Commands;

public class DebugCommand
(
    IDebugWindow debugWindow,
    BossModMiscAiBackend bossModMiscAi,
    CombatAiPresetNaming presetNaming,
    IPlayer player,
    IChatGui chat,
    UIConfig uiConfig,
    ITranslator<DebugCommand> translator
) : OcelotCommand(translator)
{
    public override string Command => "debug";

    public override List<string> Aliases => [];

    public override void Execute(CommandContext context)
    {
        if (context.Args.Length == 0)
        {
            debugWindow.Toggle();
            return;
        }

        switch (context.Args[0].ToLowerInvariant())
        {
            case "ai-preset":
            case "make-ai-preset":
                MakeAiPreset();
                break;
            case "open":
                debugWindow.IsOpen = true;
                break;
            case "close":
                debugWindow.IsOpen = false;
                break;
            case "toggle":
                debugWindow.Toggle();
                break;
            case "pos":
            case "position":
                PrintPosition();
                break;
            default:
                chat.PrintError("Usage: /bocchi debug [open|close|toggle|ai-preset|pos]");
                break;
        }
    }

    /// <summary>
    ///     Print the player position as a TreasureHuntPathOverrides via-point literal. Stand on the
    ///     safe line, run the command, paste the line.
    /// </summary>
    private void PrintPosition()
    {
        Vector3 p = player.Position;
        BocchiChat.Print(
            chat,
            uiConfig,
            $"new({p.X.ToString("0.###", CultureInfo.InvariantCulture)}f, "
            + $"{p.Y.ToString("0.###", CultureInfo.InvariantCulture)}f, "
            + $"{p.Z.ToString("0.###", CultureInfo.InvariantCulture)}f),");
    }

    private void MakeAiPreset()
    {
        var job = player.GetClassJob();
        BocchiChat.Print(
            chat,
            uiConfig,
            $"Base job={job?.Abbreviation.ToString() ?? "?"} Role={job?.Role.ToString() ?? "?"} "
            + $"IsMelee={player.IsMelee()} IsMeleeDps={player.IsMeleeDps()}");

        if (!bossModMiscAi.TryEnsurePresets(out string? storedJson))
        {
            BocchiChat.PrintError(chat, uiConfig, "Failed to create BOCCHI AI preset (is BossMod / BMR loaded?)");
            return;
        }

        BocchiChat.Print(
            chat,
            uiConfig,
            $"Created/updated presets '{presetNaming.FateMiscAi}' and '{presetNaming.CeMiscAi}'.");
        if (string.IsNullOrWhiteSpace(storedJson))
        {
            BocchiChat.PrintError(
                chat,
                uiConfig,
                "Preset Create succeeded but Get returned empty — check BossMod Presets IPC.");
            return;
        }

        BocchiChat.Print(chat, uiConfig, $"Stored JSON:\n{storedJson}");
    }
}
