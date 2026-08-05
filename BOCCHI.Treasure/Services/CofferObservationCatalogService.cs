using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BOCCHI.Treasure.Services;

public readonly record struct CrowdsourcedCofferCandidate(
    int CandidateId,
    ushort TerritoryId,
    uint DataId,
    Vector3 Position);

/// <summary>
///     Fetches accepted coffer candidates for hunt routing. Empty / failed fetch → hunt keeps authored map.
/// </summary>
public sealed class CofferObservationCatalogService
(
    TreasureConfig config,
    IZoneProvider zones,
    ILogger<CofferObservationCatalogService> logger
) : IOnUpdate
{
    public const string ApiBaseUrl = "https://bocchi-coffer-api.kagekazu.workers.dev";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly object gate = new();

    private List<CrowdsourcedCofferCandidate> cached = [];

    private ushort cachedTerritory;

    private DateTime nextRefreshUtc = DateTime.MinValue;

    private bool refreshInFlight;

    public void Update()
    {
        if (!config.EnableCofferObservationSubmission || !zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        ushort territory = zones.GetZone().TerritoryType;
        if (DateTime.UtcNow < nextRefreshUtc && territory == cachedTerritory)
        {
            return;
        }

        RequestRefresh(territory, force: false);
    }

    /// <summary>Kick a refresh before planning a hunt (non-blocking if already recent).</summary>
    public void EnsureFreshForHunt()
    {
        if (!config.EnableCofferObservationSubmission || !zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        RequestRefresh(zones.GetZone().TerritoryType, force: true);
    }

    public IReadOnlyList<CrowdsourcedCofferCandidate> GetAcceptedForCurrentZone()
    {
        if (!config.EnableCofferObservationSubmission)
        {
            return [];
        }

        ushort territory = zones.GetZone().TerritoryType;
        lock (gate)
        {
            return cachedTerritory == territory ? cached : [];
        }
    }

    private void RequestRefresh(ushort territoryId, bool force)
    {
        lock (gate)
        {
            if (refreshInFlight)
            {
                return;
            }

            if (!force && DateTime.UtcNow < nextRefreshUtc && territoryId == cachedTerritory)
            {
                return;
            }

            refreshInFlight = true;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAsync(territoryId).ConfigureAwait(false);
            }
            finally
            {
                lock (gate)
                {
                    refreshInFlight = false;
                    nextRefreshUtc = DateTime.UtcNow + RefreshInterval;
                }
            }
        });
    }

    private async Task RefreshAsync(ushort territoryId)
    {
        string url = $"{ApiBaseUrl}/api/v1/candidates?territoryId={territoryId}";
        using HttpResponseMessage response = await Http.GetAsync(url).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.Warn("Coffer candidate catalog failed: {Status}", response.StatusCode);
            return;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        CatalogResponse? payload = await JsonSerializer
            .DeserializeAsync<CatalogResponse>(stream, JsonOptions)
            .ConfigureAwait(false);

        List<CrowdsourcedCofferCandidate> next = [];
        if (payload?.Candidates != null)
        {
            foreach (CatalogCandidate entry in payload.Candidates)
            {
                if (entry.Position == null)
                {
                    continue;
                }

                next.Add(new CrowdsourcedCofferCandidate(
                    entry.CandidateId,
                    (ushort)entry.TerritoryId,
                    (uint)entry.DataId,
                    new Vector3(entry.Position.X, entry.Position.Y, entry.Position.Z)));
            }
        }

        lock (gate)
        {
            cachedTerritory = territoryId;
            cached = next;
        }

        logger.Info(
            "Coffer candidate catalog: {Count} accepted spot(s) for territory {Territory}",
            next.Count,
            territoryId);
    }

    private sealed class CatalogResponse
    {
        [JsonPropertyName("candidates")]
        public List<CatalogCandidate>? Candidates { get; set; }
    }

    private sealed class CatalogCandidate
    {
        [JsonPropertyName("candidateId")]
        public int CandidateId { get; set; }

        [JsonPropertyName("territoryId")]
        public int TerritoryId { get; set; }

        [JsonPropertyName("dataId")]
        public int DataId { get; set; }

        [JsonPropertyName("position")]
        public CatalogPosition? Position { get; set; }
    }

    private sealed class CatalogPosition
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }
    }
}
