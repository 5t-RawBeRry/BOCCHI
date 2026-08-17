using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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

public unsafe class DebugCommand
(
    IDebugWindow debugWindow,
    BossModMiscAiBackend bossModMiscAi,
    CombatAiPresetNaming presetNaming,
    IPlayer player,
    IObjectTable objects,
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
            case "chests":
                PrintNearbyChests();
                break;
            case "instance":
                PrintInstance();
                break;
            default:
                chat.PrintError("Usage: /bocchi debug [open|close|toggle|ai-preset|pos|chests]");
                break;
        }
    }

    /// <summary>
    ///     Dump nearby objects with their BaseId. Pot reveal detection only matches the BaseIds in
    ///     PotTreasureIds.RevealCofferBaseIds, so stand next to a revealed chest and run this to see
    ///     what it actually is.
    /// </summary>
    private unsafe void PrintInstance()
    {
        UIState* ui = UIState.Instance();
        BocchiChat.Print(
            chat,
            uiConfig,
            ui == null
                ? "UIState unavailable."
                : $"IsInstancedArea={ui->PublicInstance.IsInstancedArea()} InstanceId={ui->PublicInstance.InstanceId}");
    }

    private void PrintNearbyChests()
    {
        Vector3 me = player.Position;
        var near = objects
            .Where(o => o.IsValid() && !o.IsDead)
            .Select(o => (Obj: o, Dist: Vector2.Distance(
                new Vector2(me.X, me.Z),
                new Vector2(o.Position.X, o.Position.Z))))
            .Where(x => x.Dist <= 30f)
            .OrderBy(x => x.Dist)
            .Take(15)
            .ToList();

        if (near.Count == 0)
        {
            BocchiChat.Print(chat, uiConfig, "No objects within 30y.");
            return;
        }

        BocchiChat.Print(chat, uiConfig, $"Objects within 30y (BaseId / kind / name / distance):");
        foreach ((IGameObject obj, float dist) in near)
        {
            BocchiChat.Print(
                chat,
                uiConfig,
                $"  {obj.BaseId}  {obj.ObjectKind}  \"{obj.Name.TextValue}\"  {dist:0.#}y");
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
