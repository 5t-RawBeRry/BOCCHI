using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Treasure.Hunt;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Ocelot.Services.UI;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TreasureSheet = Lumina.Excel.Sheets.Treasure;

namespace BOCCHI.Debug.Panels;

/// <summary>Exports bronze/silver layout pads to <c>treasure_locations.json</c>.</summary>
public sealed class TreasureLocationsExportPanel
(
    IZoneProvider zones,
    IDataManager data,
    IDalamudPluginInterface plugin,
    IPluginLog log,
    IBrandingService branding,
    IUIService ui
) : IDebugPanel
{
    private const string Filename = "treasure_locations.json";

    private static readonly JsonSerializerOptions WriteJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<LayoutTreasure> treasures = [];

    private string? lastError;

    private string? lastOutputPath;

    private string? lastRefreshSummary;

    public string Name => "Treasure Locations Export";

    public void Render()
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            ui.Text("Enter South Horn or North Horn first.");
            return;
        }

        List<TreasureData> authored = zone.GetTreasureData();

        ui.Text(
            "Reads bronze/silver pads from the game layout, then writes treasure_locations.json.",
            branding.DalamudGrey);
        ui.Text("No Worker — layout already has exact coffer positions.", branding.DalamudGrey);

        if (lastError != null)
        {
            ui.Text(lastError, branding.DalamudRed);
        }

        if (lastRefreshSummary != null)
        {
            ui.Text(lastRefreshSummary, branding.DalamudYellow);
        }

        ui.LabelledValue("Layout spots", treasures.Count);
        ui.LabelledValue("Bronze", treasures.Count(t => t.SgbId == TreasureCoffer.BronzeSgbId));
        ui.LabelledValue("Silver", treasures.Count(t => t.SgbId == TreasureCoffer.SilverSgbId));
        ui.LabelledValue("Authored", authored.Count);

        if (treasures.Count > 0 && authored.Count > 0)
        {
            int matched = treasures.Count(t => FindAuthoredLevel(t, authored) != null);
            ui.LabelledValue("Level matched", matched);
        }

        if (ImGui.Button("Refresh from layout"))
        {
            try
            {
                RefreshFromLayout(zone);
                lastError = null;
                lastRefreshSummary =
                    $"Loaded {treasures.Count} layout coffers ({treasures.Count(t => t.SgbId == TreasureCoffer.BronzeSgbId)} bronze / {treasures.Count(t => t.SgbId == TreasureCoffer.SilverSgbId)} silver).";
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                lastRefreshSummary = null;
                log.Error(ex, "[TreasureLocationsExport] Refresh failed");
            }
        }

        if (lastOutputPath != null)
        {
            ui.Text($"Wrote {lastOutputPath}", branding.DalamudYellow);
        }

        if (treasures.Count == 0)
        {
            return;
        }

        if (ImGui.Button("Write treasure_locations.json"))
        {
            try
            {
                List<string> written = WriteJsonForZone(zone, treasures, authored);
                lastOutputPath = string.Join(" | ", written);
                lastError = null;
                log.Information("[TreasureLocationsExport] Wrote {Paths}", lastOutputPath);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                log.Error(ex, "[TreasureLocationsExport] Write failed");
            }
        }
    }

    private unsafe void RefreshFromLayout(IZone zone)
    {
        treasures.Clear();
        LayoutManager* layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null)
        {
            throw new InvalidOperationException("No active layout.");
        }

        if (!layout->InstancesByType.TryGetValue(
                InstanceType.Treasure,
                out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> mapPtr,
                false))
        {
            throw new InvalidOperationException("No treasure layout instances.");
        }

        List<TreasureData> authored = zone.GetTreasureData();
        bool hasPositionData = authored.Exists(d => d.Position.HasValue);
        var sheet = data.GetExcelSheet<TreasureSheet>();

        foreach (ILayoutInstance* instance in mapPtr.Value->Values)
        {
            Transform* transform = instance->GetTransformImpl();
            Vector3 position = transform->Translation;
            if (position.Y <= -10f && !hasPositionData)
            {
                continue;
            }

            uint treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
            if (!sheet.TryGetRow(treasureRowId, out TreasureSheet row))
            {
                continue;
            }

            uint sgbId = row.SGB.RowId;
            if (!TreasureCoffer.IsBronzeOrSilverSgb(sgbId))
            {
                continue;
            }

            if (hasPositionData && !authored.Any(d => d.Matches(treasureRowId, position)))
            {
                continue;
            }

            treasures.Add(new LayoutTreasure(treasureRowId, position, sgbId));
        }

        treasures.Sort((a, b) => a.DataId.CompareTo(b.DataId));
    }

    private List<string> WriteJsonForZone(
        IZone zone,
        List<LayoutTreasure> spots,
        List<TreasureData> authored)
    {
        string zoneFolder = zone.ZoneId.TreasureDataFolder();

        List<TreasureJsonEntry> entries = [];
        int id = 1;
        foreach (LayoutTreasure spot in spots)
        {
            entries.Add(new TreasureJsonEntry
            {
                Id = id++,
                DataId = spot.DataId,
                X = spot.Position.X,
                Y = spot.Position.Y,
                Z = spot.Position.Z,
                SgbId = spot.SgbId,
                Level = FindAuthoredLevel(spot, authored),
            });
        }

        var payload = new TreasureLocationsFile
        {
            SchemaVersion = 1,
            TerritoryId = zone.TerritoryType,
            Zone = zoneFolder,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            Source = "layout",
            Treasures = entries,
        };

        string json = JsonSerializer.Serialize(payload, WriteJson);
        return TreasureDataPaths.WriteZoneDataFile(
            plugin.AssemblyLocation.DirectoryName,
            zoneFolder,
            Filename,
            json);
    }

    private static int? FindAuthoredLevel(LayoutTreasure spot, List<TreasureData> authored)
    {
        TreasureData? byId = authored.FirstOrDefault(a => a.Id == spot.DataId);
        if (byId != null)
        {
            return byId.Level;
        }

        TreasureData? nearest = null;
        float bestDistSq = HuntDistances.LayoutProximityRadiusSq;
        foreach (TreasureData entry in authored)
        {
            if (entry.Position is not { } pos)
            {
                continue;
            }

            float distSq = Vector3.DistanceSquared(pos, spot.Position);
            if (distSq > bestDistSq)
            {
                continue;
            }

            bestDistSq = distSq;
            nearest = entry;
        }

        return nearest?.Level;
    }

    private readonly record struct LayoutTreasure(uint DataId, Vector3 Position, uint SgbId);

    private sealed class TreasureLocationsFile
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("territoryId")]
        public int TerritoryId { get; set; }

        [JsonPropertyName("zone")]
        public string Zone { get; set; } = "";

        [JsonPropertyName("generatedAtUtc")]
        public string GeneratedAtUtc { get; set; } = "";

        [JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonPropertyName("treasures")]
        public List<TreasureJsonEntry> Treasures { get; set; } = [];
    }

    private sealed class TreasureJsonEntry
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("dataId")]
        public uint DataId { get; set; }

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        [JsonPropertyName("sgbId")]
        public uint SgbId { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }
    }
}
