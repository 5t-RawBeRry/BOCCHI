using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using BOCCHI.Debug;
using Dalamud.Plugin.Services;
using Ocelot.Rotation.Services;
using Ocelot.Rotation.Services.BossMod;
using Ocelot.Services.Commands;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using GameDynamicEvent = FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.DynamicEvent;

namespace BOCCHI.Commands;

public unsafe class DebugCommand
(
    IDebugWindow debugWindow,
    BossModMiscAiBackend bossModMiscAi,
    CombatAiPresetNaming presetNaming,
    IPlayer player,
    IObjectTable objects,
    IZoneProvider zones,
    IDataManager data,
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
            case "currency":
                PrintCurrency();
                break;
            case "ce":
                PrintCriticalEncounterMeasurement();
                break;
            default:
                chat.PrintError("Usage: /bocchi debug [open|close|toggle|ai-preset|pos|chests|instance|currency|ce]");
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

    /// <summary>
    ///     Measure a Critical Encounter's real registration area. Stand on the blue rim and run this
    ///     at a few points around it; the largest reading is the radius to author.
    ///     <para>
    ///     Needed because every CE radius is hand-authored and most are still the default 20y, while
    ///     the game exposes no geometry to read instead — neither DynamicEvent nor MycDynamicEvent
    ///     carries a position or radius. A CE whose real ring is wider than the authored value gets
    ///     you repathed inward while you are legitimately already inside it.
    ///     </para>
    /// </summary>
    private void PrintCriticalEncounterMeasurement()
    {
        List<ActivityData> encounters = zones.GetZone().GetCriticalEncounterData();
        if (encounters.Count == 0)
        {
            BocchiChat.Print(chat, uiConfig, "No authored Critical Encounters in this zone.");
            return;
        }

        Vector3 me = player.Position;
        ActivityData nearest = encounters.MinBy(ce => Flat(me, ce.Position))!;

        float dx = MathF.Abs(me.X - nearest.Position.X);
        float dz = MathF.Abs(me.Z - nearest.Position.Z);
        float circle = Flat(me, nearest.Position);
        float square = MathF.Max(dx, dz);
        float authored = nearest.CombatRadius ?? 0f;
        float measured = nearest.AreaShape == ActivityAreaShape.Square ? square : circle;

        BocchiChat.Print(
            chat,
            uiConfig,
            $"CE {nearest.Id} ({nearest.AreaShape}) centre <{nearest.Position.X:0.#}, {nearest.Position.Z:0.#}>  authored {authored:0.#}y");
        BocchiChat.Print(
            chat,
            uiConfig,
            $"  you are {measured:0.#}y out (circle {circle:0.#}y / square half-extent {square:0.#}y)"
            + $"  → {(measured > authored ? "OUTSIDE" : "inside")} the authored area");
        // InvariantCulture: this line is meant to be pasted into source, and a locale decimal comma
        // turns "29.8f" into "29,8f" — which reads as two arguments and will not compile.
        BocchiChat.Print(
            chat,
            uiConfig,
            "  " + string.Format(
                CultureInfo.InvariantCulture,
                "new({0}, new({1:0.###}f, {2:0.###}f, {3:0.###}f), {4:0.#}f),",
                nearest.Id,
                nearest.Position.X,
                nearest.Position.Y,
                nearest.Position.Z,
                measured));

        PrintLiveEventGeometry();
    }

    /// <summary>
    ///     Report the centre and radius the game itself holds for each live Critical Encounter.
    ///     <c>DynamicEvent.MapMarker</c> carries both, so if these agree with what we author by hand
    ///     the whole authored table could be replaced by reading this at runtime — no measuring, and
    ///     no drift when a zone is adjusted.
    /// </summary>
    private void PrintLiveEventGeometry()
    {
        PublicContentOccultCrescent* content = PublicContentOccultCrescent.GetInstance();
        if (content == null)
        {
            BocchiChat.Print(chat, uiConfig, "  (Occult content director unavailable — no live event data)");
            return;
        }

        BocchiChat.Print(chat, uiConfig, "Live DynamicEvent markers (id / state / centre / radius):");
        ref DynamicEventContainer container = ref content->DynamicEventContainer;

        var any = false;
        for (var i = 0; i < container.Events.Length; i++)
        {
            GameDynamicEvent evt = container.Events[i];
            if (evt.DynamicEventId == 0)
            {
                continue;
            }

            any = true;
            BocchiChat.Print(
                chat,
                uiConfig,
                $"  {evt.DynamicEventId}  {evt.State}  "
                + $"<{evt.MapMarker.Position.X:0.#}, {evt.MapMarker.Position.Z:0.#}>  r={evt.MapMarker.Radius:0.#}y");
        }

        if (!any)
        {
            BocchiChat.Print(chat, uiConfig, "  (no events populated right now)");
        }
    }

    /// <summary>
    ///     Prints the raw currency source the per-hour trackers read. Compare against the in-game
    ///     Enlightenment counters: matching numbers mean the source is fine and any wrong rate is in
    ///     the rate logic; zeroes or nonsense mean the tracker is reading the wrong field.
    /// </summary>
    private void PrintCurrency()
    {
        BocchiChat.Print(
            chat,
            uiConfig,
            $"IsStateAvailable={OccultCrescentHelper.IsStateAvailable()} "
            + $"GoldTotal={OccultCrescentHelper.GetGoldTotal()} SilverTotal={OccultCrescentHelper.GetSilverTotal()} "
            + $"(pieces {OccultCrescentHelper.GetGoldPieces()}/{OccultCrescentHelper.GetSilverPieces()})");

        // Drops mention currencies we have never heard of ("Enlightenment silver obols"), and they
        // do not live in InventoryType.Currency. Ask the game's own item sheet which Enlightenment
        // items exist and how many we hold, so the ids come from the game rather than a guess.
        InventoryManager* inventory = InventoryManager.Instance();
        BocchiChat.Print(chat, uiConfig, "Enlightenment items (itemId / name / held):");

        foreach (Item row in data.GetExcelSheet<Item>())
        {
            string name = row.Name.ExtractText();
            if (!name.Contains("Enlightenment", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int held = inventory == null ? -1 : inventory->GetInventoryItemCount(row.RowId);
            BocchiChat.Print(chat, uiConfig, $"  {row.RowId}  \"{name}\"  {held}");
        }
    }

    /// <summary>
    ///     Lists nearby objects and, for treasure ones, says whether the pot-reveal filter would
    ///     accept them and why not. Standing on a revealed pot chest and running this answers the
    ///     question the logs cannot: is the coffer being rejected, or never seen at all?
    /// </summary>
    private void PrintNearbyChests()
    {
        Vector3 me = player.Position;
        var near = objects
            .Where(o => o.IsValid() && !o.IsDead)
            .Select(o => (Obj: o, Dist: Flat(me, o.Position)))
            .Where(x => x.Dist <= 30f)
            .OrderBy(x => x.Dist)
            .Take(15)
            .ToList();

        if (near.Count == 0)
        {
            BocchiChat.Print(chat, uiConfig, "No objects within 30y.");
            return;
        }

        IZone zone = zones.GetZone();
        List<Vector3> potSpots = zone.GetPotChestData().Values
            .SelectMany(chests => chests.Select(c => c.Position))
            .Concat(zone.GetRerollPotChestData().Select(c => c.Position))
            .ToList();
        List<Vector3> huntSpots = zone.GetTreasureData()
            .Where(t => t.Position.HasValue)
            .Select(t => t.Position!.Value)
            .ToList();

        BocchiChat.Print(chat, uiConfig, "Objects within 30y (BaseId / kind / name / distance):");
        foreach ((IGameObject obj, float dist) in near)
        {
            BocchiChat.Print(
                chat,
                uiConfig,
                $"  {obj.BaseId}  {obj.ObjectKind}  \"{obj.Name.TextValue}\"  {dist:0.#}y"
                + (obj.ObjectKind == DalamudObjectKind.Treasure
                    ? $"  targetable={obj.IsTargetable}  {ClassifyReveal(obj, potSpots, huntSpots)}"
                    : string.Empty));
        }
    }

    /// <summary>Mirrors FarmingPotChestsHandler's reveal gate so the verdict here matches the farm.</summary>
    private static string ClassifyReveal(IGameObject obj, List<Vector3> potSpots, List<Vector3> huntSpots)
    {
        float pot = potSpots.Count == 0 ? float.MaxValue : potSpots.Min(p => Flat(obj.Position, p));
        float hunt = huntSpots.Count == 0 ? float.MaxValue : huntSpots.Min(p => Flat(obj.Position, p));
        string distances = $"pot={pot:0.#}y hunt={hunt:0.#}y";

        if (pot > 12f)
        {
            return $"REJECT (not on a pot spot; {distances})";
        }

        return hunt < pot
            ? $"REJECT (nearer a hunt coffer; {distances})"
            : $"ACCEPT as pot reveal ({distances})";
    }

    private static float Flat(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

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
