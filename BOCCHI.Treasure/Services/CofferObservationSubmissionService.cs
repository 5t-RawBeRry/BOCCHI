using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace BOCCHI.Treasure.Services;

/// <summary>
///     Opt-in anonymous coffer observations for the BOCCHI Worker.
///     Payload is AOCC-compatible; endpoint is fixed (not user-configurable).
/// </summary>
public sealed class CofferObservationSubmissionService
(
    TreasureConfig config,
    IZoneProvider zones,
    IDalamudPluginInterface plugin,
    ILogger<CofferObservationSubmissionService> logger
) : IOnUpdate
{
    public const string ApiUrl = "https://bocchi-coffer-api.kagekazu.workers.dev/api/v1/observations";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly Queue<PendingObservation> queue = new();

    private DateTime nextAttemptUtc = DateTime.MinValue;

    public void Submit(uint dataId, float x, float y, float z, string cofferType)
    {
        if (!config.EnableCofferObservationSubmission || !zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        queue.Enqueue(new PendingObservation
        {
            TerritoryId = zones.GetZone().TerritoryType,
            DataId = dataId,
            WorldX = x,
            WorldY = y,
            WorldZ = z,
            CofferType = cofferType,
            ObservedAtUtc = DateTime.UtcNow,
        });
    }

    public void Update()
    {
        if (!config.EnableCofferObservationSubmission || queue.Count == 0)
        {
            return;
        }

        if (DateTime.UtcNow < nextAttemptUtc)
        {
            return;
        }

        PendingObservation obs = queue.Peek();
        try
        {
            string json = JsonSerializer.Serialize(new
            {
                territoryId = obs.TerritoryId,
                dataId = obs.DataId,
                worldX = obs.WorldX,
                worldY = obs.WorldY,
                worldZ = obs.WorldZ,
                cofferType = obs.CofferType,
                installationHash = GetInstallationHash(),
                pluginVersion = typeof(CofferObservationSubmissionService).Assembly.GetName().Version?.ToString() ?? "0",
                observedAtUtc = obs.ObservedAtUtc.ToString("O"),
            });

            using HttpRequestMessage request = new(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response = Http.Send(request);
            if (response.IsSuccessStatusCode)
            {
                queue.Dequeue();
                nextAttemptUtc = DateTime.UtcNow;
            }
            else
            {
                logger.Warn("Coffer observation rejected: {Status}", response.StatusCode);
                nextAttemptUtc = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            }
        }
        catch (Exception ex)
        {
            logger.Warn("Coffer observation failed: {Message}", ex.Message);
            nextAttemptUtc = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        }
    }

    private string GetInstallationHash()
    {
        string path = Path.Combine(plugin.ConfigDirectory.FullName, "coffer-installation-id.txt");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, Guid.NewGuid().ToString("N"));
        }

        string id = File.ReadAllText(path).Trim();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant();
    }

    private sealed class PendingObservation
    {
        public ushort TerritoryId { get; init; }

        public uint DataId { get; init; }

        public float WorldX { get; init; }

        public float WorldY { get; init; }

        public float WorldZ { get; init; }

        public string CofferType { get; init; } = "";

        public DateTime ObservedAtUtc { get; init; }
    }
}
