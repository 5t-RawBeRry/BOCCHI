using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Treasure.Hunt;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Ocelot.Services.UI;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BOCCHI.Debug.Panels;

/// <summary>
///     Pulls accepted carrot pads from the Worker catalog and writes
///     <c>carrot_locations.json</c> once the zone has enough reports.
/// </summary>
public sealed class CarrotLocationsExportPanel
(
    IZoneProvider zones,
    CarrotLocationSyncService carrotLocations,
    IDalamudPluginInterface plugin,
    IPluginLog log,
    IBrandingService branding,
    IUIService ui
) : IDebugPanel
{
    private const string Filename = "carrot_locations.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly JsonSerializerOptions ReadJson = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private List<CatalogLocation> locations = [];

    private string? lastError;

    private string? lastOutputPath;

    private string? lastFetchSummary;

    private bool forceWrite;

    private bool fetching;

    public string Name => "Carrot Locations Export";

    public void Render()
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            ui.Text("Enter South Horn or North Horn first.");
            return;
        }

        List<CarrotData> authored = zone.GetCarrotData();
        int required = authored.Count > 0 ? authored.Count : 20;

        ui.Text("Fetches accepted pads from the BOCCHI Worker, then writes carrot_locations.json.", branding.DalamudGrey);
        ui.Text($"Need at least {required} accepted locations for this zone (authored map size).", branding.DalamudGrey);

        if (lastError != null)
        {
            ui.Text(lastError, branding.DalamudRed);
        }

        if (lastFetchSummary != null)
        {
            ui.Text(lastFetchSummary, branding.DalamudYellow);
        }

        ui.LabelledValue("Cached (sync)", carrotLocations.AcceptedLocations.Count);
        ui.LabelledValue("Fetched", locations.Count);
        ui.LabelledValue("Required", required);
        ui.LabelledValue("Authored", authored.Count);

        if (locations.Count > 0 && authored.Count > 0)
        {
            int matched = locations.Count(l => FindNearestAuthored(l.Position, authored) != null);
            ui.LabelledValue("Near authored (≤80y)", matched);
        }

        if (fetching)
        {
            ui.Text("Fetching…");
            return;
        }

        if (ImGui.Button("Fetch accepted catalog"))
        {
            _ = FetchAsync(zone);
        }

        ImGui.SameLine();
        if (ImGui.Button("Use sync cache") && carrotLocations.AcceptedLocations.Count > 0)
        {
            locations = carrotLocations.AcceptedLocations
                .Where(l => l.TerritoryId == zone.TerritoryType)
                .Select(l => new CatalogLocation(l.CandidateId, l.TerritoryId, l.Position))
                .OrderBy(l => l.CandidateId)
                .ToList();
            lastError = null;
            lastFetchSummary = $"Loaded {locations.Count} from sync cache.";
        }

        ImGui.Checkbox("Force write (ignore count)", ref forceWrite);

        bool enough = locations.Count >= required;
        bool canWrite = locations.Count > 0 && (enough || forceWrite);
        if (!enough && locations.Count > 0 && !forceWrite)
        {
            ui.Text($"Not enough yet ({locations.Count}/{required}). Keep collecting, or force write.", branding.DalamudYellow);
        }

        if (lastOutputPath != null)
        {
            ui.Text($"Wrote {lastOutputPath}", branding.DalamudYellow);
        }

        if (!canWrite)
        {
            return;
        }

        if (ImGui.Button("Write carrot_locations.json"))
        {
            try
            {
                List<string> written = WriteJsonForZone(zone, locations, authored);
                lastOutputPath = string.Join(" | ", written);
                lastError = null;
                log.Information("[CarrotLocationsExport] Wrote {Paths}", lastOutputPath);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                log.Error(ex, "[CarrotLocationsExport] Write failed");
            }
        }
    }

    private async Task FetchAsync(IZone zone)
    {
        fetching = true;
        lastError = null;
        lastOutputPath = null;
        try
        {
            ushort territory = zone.TerritoryType;
            string url = $"{CarrotLocationSyncService.ApiUrl}?territoryId={territory}";
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            HttpResponseMessage response = await Http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                lastError = $"Fetch failed: {(int)response.StatusCode}";
                return;
            }

            CatalogResponse? parsed = JsonSerializer.Deserialize<CatalogResponse>(body, ReadJson);
            locations = parsed?.Locations?
                .Where(l => l.TerritoryId == territory && l.Position != null)
                .Select(l => new CatalogLocation(
                    l.CandidateId,
                    (ushort)l.TerritoryId,
                    new Vector3(l.Position!.X, l.Position.Y, l.Position.Z)))
                .OrderBy(l => l.CandidateId)
                .ToList()
                ?? [];

            lastFetchSummary = $"Fetched {locations.Count} accepted pads for territory {territory}.";
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            log.Error(ex, "[CarrotLocationsExport] Fetch failed");
        }
        finally
        {
            fetching = false;
        }
    }

    private List<string> WriteJsonForZone(IZone zone, List<CatalogLocation> pads, List<CarrotData> authored)
    {
        string zoneFolder = zone.ZoneId switch
        {
            ZoneId.SouthHorn => "SouthHorn",
            ZoneId.NorthHorn => "NorthHorn",
            var _ => throw new NotSupportedException($"No carrot data folder for {zone.ZoneId}"),
        };

        List<CarrotJsonEntry> carrots = [];
        int id = 1;
        foreach (CatalogLocation pad in pads)
        {
            CarrotData? nearest = FindNearestAuthored(pad.Position, authored);
            carrots.Add(new CarrotJsonEntry
            {
                Id = id++,
                CandidateId = pad.CandidateId,
                X = pad.Position.X,
                Y = pad.Position.Y,
                Z = pad.Position.Z,
                Level = nearest?.Level,
            });
        }

        var payload = new CarrotLocationsFile
        {
            SchemaVersion = 1,
            TerritoryId = zone.TerritoryType,
            Zone = zoneFolder,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            Source = "worker-accepted",
            Carrots = carrots,
        };

        string json = JsonSerializer.Serialize(payload, WriteJson);
        List<string> written = [];

        string? pluginDir = plugin.AssemblyLocation.DirectoryName;
        if (!string.IsNullOrEmpty(pluginDir))
        {
            string runtimePath = Path.Combine(pluginDir, "Data", zoneFolder, Filename);
            Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
            File.WriteAllText(runtimePath, json);
            written.Add(runtimePath);
        }

        string? sourceRoot = FindRepoTreasureDataRoot(pluginDir);
        if (sourceRoot != null)
        {
            string sourcePath = Path.Combine(sourceRoot, zoneFolder, Filename);
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, json);
            written.Add(sourcePath);
        }

        if (written.Count == 0)
        {
            throw new InvalidOperationException("Could not resolve a Data folder to write into.");
        }

        return written;
    }

    private static CarrotData? FindNearestAuthored(Vector3 position, List<CarrotData> authored)
    {
        CarrotData? best = null;
        float bestDistSq = CarrotHuntDistances.MatchRadius * CarrotHuntDistances.MatchRadius;
        foreach (CarrotData pad in authored)
        {
            float distSq = Vector3.DistanceSquared(position, pad.Position);
            if (distSq > bestDistSq)
            {
                continue;
            }

            bestDistSq = distSq;
            best = pad;
        }

        return best;
    }

    private static string? FindRepoTreasureDataRoot(string? start)
    {
        string? dir = start;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "BOCCHI.Treasure", "Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private readonly record struct CatalogLocation(int CandidateId, ushort TerritoryId, Vector3 Position);

    private sealed class CatalogResponse
    {
        [JsonPropertyName("locations")]
        public List<CatalogLocationDto>? Locations { get; set; }
    }

    private sealed class CatalogLocationDto
    {
        [JsonPropertyName("candidateId")]
        public int CandidateId { get; set; }

        [JsonPropertyName("territoryId")]
        public int TerritoryId { get; set; }

        [JsonPropertyName("position")]
        public PositionDto? Position { get; set; }
    }

    private sealed class PositionDto
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }
    }

    private sealed class CarrotLocationsFile
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

        [JsonPropertyName("carrots")]
        public List<CarrotJsonEntry> Carrots { get; set; } = [];
    }

    private sealed class CarrotJsonEntry
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("candidateId")]
        public int CandidateId { get; set; }

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }
    }
}
