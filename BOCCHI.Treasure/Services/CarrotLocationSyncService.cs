using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Data;
using Dalamud.Plugin;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using System.Globalization;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BOCCHI.Treasure.Services;

/// <summary>
///     Anonymous chewed-carrot pad sync for the BOCCHI Worker.
///     Submits live carrot positions and caches the accepted catalog for mesh-bake / map work.
/// </summary>
public sealed class CarrotLocationSyncService
(
    IZoneProvider zones,
    ICarrotTracker carrots,
    IDalamudPluginInterface plugin,
    ILogger<CarrotLocationSyncService> logger
) : IOnUpdate
{
    public const string ApiBaseUrl = "https://bocchi-coffer-api.kagekazu.workers.dev";

    public const string ApiUrl = ApiBaseUrl + "/api/v1/carrot-locations";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan CatalogRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly Queue<PendingSubmit> queue = new();

    private readonly HashSet<string> queuedKeys = new(StringComparer.Ordinal);

    private readonly HashSet<string> submittedKeys = new(StringComparer.Ordinal);

    private DateTime nextUploadAttemptUtc = DateTime.MinValue;

    private DateTime nextCatalogFetchUtc = DateTime.MinValue;

    private ushort catalogTerritory;

    public IReadOnlyList<AcceptedCarrotLocation> AcceptedLocations { get; private set; } = [];

    /// <summary>After <see cref="CarrotTracker"/> (default Order 0).</summary>
    public int Order => -10;

    public UpdateLimit UpdateLimit =>
        new()
        {
            Mode = UpdateLimitMode.Milliseconds,
            Limit = 1000
        };

    public void Update()
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            return;
        }

        ushort territory = zone.TerritoryType;
        EnqueueSightedCarrots(territory);
        FlushQueue();
        MaybeRefreshCatalog(territory);
    }

    private void EnqueueSightedCarrots(ushort territory)
    {
        foreach (Carrot carrot in carrots.Carrots)
        {
            if (!carrot.IsValid())
            {
                continue;
            }

            Vector3 position = carrot.GetPosition();
            string key = PositionKey(territory, position);
            if (queuedKeys.Contains(key) || submittedKeys.Contains(key))
            {
                continue;
            }

            queue.Enqueue(new PendingSubmit(territory, position, key));
            queuedKeys.Add(key);
        }
    }

    private void FlushQueue()
    {
        if (queue.Count == 0 || DateTime.UtcNow < nextUploadAttemptUtc)
        {
            return;
        }

        PendingSubmit pending = queue.Peek();
        try
        {
            string json = JsonSerializer.Serialize(new
            {
                territoryId = (int)pending.TerritoryId,
                worldX = pending.Position.X,
                worldY = pending.Position.Y,
                worldZ = pending.Position.Z,
                objectBaseId = (int)OccultObjectType.Carrot,
                installationHash = InstallationId.GetHash(plugin),
                pluginVersion = typeof(CarrotLocationSyncService).Assembly.GetName().Version?.ToString() ?? "0",
                observedAtUtc = DateTime.UtcNow.ToString("O"),
            });

            using HttpRequestMessage request = new(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response = Http.Send(request);
            if (response.IsSuccessStatusCode)
            {
                queue.Dequeue();
                queuedKeys.Remove(pending.Key);
                submittedKeys.Add(pending.Key);
                nextUploadAttemptUtc = DateTime.UtcNow;
                logger.Info(
                    "[CarrotLocationSync] uploaded territory={Territory} pos=({X:F2},{Y:F2},{Z:F2})",
                    pending.TerritoryId,
                    pending.Position.X,
                    pending.Position.Y,
                    pending.Position.Z);
            }
            else
            {
                logger.Warn("[CarrotLocationSync] upload rejected: {Status}", response.StatusCode);
                nextUploadAttemptUtc = DateTime.UtcNow + RetryDelay;
            }
        }
        catch (Exception ex)
        {
            logger.Warn("[CarrotLocationSync] upload failed: {Message}", ex.Message);
            nextUploadAttemptUtc = DateTime.UtcNow + RetryDelay;
        }
    }

    private void MaybeRefreshCatalog(ushort territory)
    {
        if (catalogTerritory == territory
            && DateTime.UtcNow < nextCatalogFetchUtc
            && AcceptedLocations.Count > 0)
        {
            return;
        }

        if (DateTime.UtcNow < nextCatalogFetchUtc && catalogTerritory == territory)
        {
            return;
        }

        try
        {
            string url = $"{ApiUrl}?territoryId={territory}";
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            HttpResponseMessage response = Http.Send(request);
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                logger.Warn("[CarrotLocationSync] catalog rejected: {Status}", response.StatusCode);
                nextCatalogFetchUtc = DateTime.UtcNow + RetryDelay;
                return;
            }

            CarrotCatalogResponse? parsed = JsonSerializer.Deserialize<CarrotCatalogResponse>(body, JsonOptions);
            AcceptedLocations = parsed?.Locations?
                .Where(l => l.TerritoryId == territory && l.Position != null)
                .Select(l => new AcceptedCarrotLocation(
                    l.CandidateId,
                    (ushort)l.TerritoryId,
                    new Vector3(l.Position!.X, l.Position.Y, l.Position.Z)))
                .ToList()
                ?? [];

            catalogTerritory = territory;
            nextCatalogFetchUtc = DateTime.UtcNow + CatalogRefreshInterval;
            logger.Info(
                "[CarrotLocationSync] catalog territory={Territory} locations={Count}",
                territory,
                AcceptedLocations.Count);
        }
        catch (Exception ex)
        {
            logger.Warn("[CarrotLocationSync] catalog failed: {Message}", ex.Message);
            nextCatalogFetchUtc = DateTime.UtcNow + RetryDelay;
        }
    }

    private static string PositionKey(ushort territory, Vector3 position)
    {
        // Match Worker near-dupe window (±0.1 yalm).
        string x = MathF.Round(position.X, 1).ToString("F1", CultureInfo.InvariantCulture);
        string y = MathF.Round(position.Y, 1).ToString("F1", CultureInfo.InvariantCulture);
        string z = MathF.Round(position.Z, 1).ToString("F1", CultureInfo.InvariantCulture);
        return $"{territory}:{x}:{y}:{z}";
    }

    private readonly record struct PendingSubmit(ushort TerritoryId, Vector3 Position, string Key);

    public readonly record struct AcceptedCarrotLocation(int CandidateId, ushort TerritoryId, Vector3 Position);

    private sealed class CarrotCatalogResponse
    {
        [JsonPropertyName("locations")]
        public List<CarrotLocationDto>? Locations { get; set; }
    }

    private sealed class CarrotLocationDto
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
}
