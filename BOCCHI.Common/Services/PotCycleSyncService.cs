using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.PlayerState;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BOCCHI.Common.Services;

/// <summary>
///     Opt-in anonymous pot-cycle sync for the BOCCHI Worker.
///     Fingerprints the instance from any active FATE (Linker-style), uploads local pot anchors,
///     and fetches shared anchors when the local tracker has none yet.
/// </summary>
public sealed class PotCycleSyncService
(
    TreasureConfig config,
    IZoneProvider zones,
    IPotCycleTracker potCycles,
    IFateRepository fates,
    IPlayer player,
    IDalamudPluginInterface plugin,
    ILogger<PotCycleSyncService> logger
) : IOnUpdate
{
    public const string ApiBaseUrl = "https://bocchi-coffer-api.kagekazu.workers.dev";

    public const string ApiUrl = ApiBaseUrl + "/api/v1/pot-cycles";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan FetchRetryDelay = TimeSpan.FromSeconds(20);

    private ushort fingerprintTerritory;

    private string? instanceKey;

    private uint fingerprintFateId;

    private int fingerprintStartEpoch;

    private ushort lastUploadedTerritory;

    private int lastUploadedPotFateId;

    private long lastUploadedSpawnUnix;

    private string? lastFetchedInstanceKey;

    private DateTime nextUploadAttemptUtc = DateTime.MinValue;

    private DateTime nextFetchAttemptUtc = DateTime.MinValue;

    public UpdateLimit UpdateLimit =>
        new()
        {
            Mode = UpdateLimitMode.Milliseconds,
            Limit = 1000
        };

    public void Update()
    {
        if (!config.EnablePotCycleSync)
        {
            ResetSession();
            return;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            ResetSession();
            return;
        }

        ushort territory = zone.TerritoryType;
        if (fingerprintTerritory != territory)
        {
            // Keep the other zone's pot timer (SH/NH are tracked separately).
            ResetFingerprint(territory);
        }

        RefreshFingerprint(territory);
        if (string.IsNullOrEmpty(instanceKey))
        {
            return;
        }

        PotCycleSnapshot snap = potCycles.Snapshot;
        TryUpload(snap, territory);
        TryFetch(snap, territory);
    }

    private void TryUpload(PotCycleSnapshot snap, ushort territory)
    {
        if (snap.TerritoryTypeId != territory
            || !snap.HasKnownAnchor
            || snap.IsRemoteAnchor
            || snap.AnchorPotFateId == 0
            || snap.AnchorSpawnAt == DateTimeOffset.MinValue)
        {
            return;
        }

        long spawnUnix = snap.AnchorSpawnAt.ToUnixTimeSeconds();
        if (lastUploadedTerritory == territory
            && lastUploadedPotFateId == snap.AnchorPotFateId
            && lastUploadedSpawnUnix == spawnUnix)
        {
            return;
        }

        if (DateTime.UtcNow < nextUploadAttemptUtc || instanceKey == null)
        {
            return;
        }

        uint? datacenterId = TryGetDatacenterId();
        if (datacenterId is not uint dc)
        {
            return;
        }

        try
        {
            string json = JsonSerializer.Serialize(new
            {
                instanceKey,
                territoryId = (int)territory,
                datacenterId = (int)dc,
                potFateId = snap.AnchorPotFateId,
                spawnAtUnix = spawnUnix,
                installationHash = GetInstallationHash(),
                pluginVersion = typeof(PotCycleSyncService).Assembly.GetName().Version?.ToString() ?? "0",
                observedAtUtc = DateTime.UtcNow.ToString("O"),
            });

            using HttpRequestMessage request = new(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response = Http.Send(request);
            if (response.IsSuccessStatusCode)
            {
                lastUploadedTerritory = territory;
                lastUploadedPotFateId = snap.AnchorPotFateId;
                lastUploadedSpawnUnix = spawnUnix;
                nextUploadAttemptUtc = DateTime.UtcNow;
                logger.Info(
                    "[PotCycleSync] uploaded pot={PotId} spawn={Spawn} key={KeyPrefix}…",
                    snap.AnchorPotFateId,
                    spawnUnix,
                    instanceKey[..8]);
            }
            else
            {
                logger.Warn("[PotCycleSync] upload rejected: {Status}", response.StatusCode);
                nextUploadAttemptUtc = DateTime.UtcNow + RetryDelay;
            }
        }
        catch (Exception ex)
        {
            logger.Warn("[PotCycleSync] upload failed: {Message}", ex.Message);
            nextUploadAttemptUtc = DateTime.UtcNow + RetryDelay;
        }
    }

    private void TryFetch(PotCycleSnapshot snap, ushort territory)
    {
        if (snap.HasKnownAnchor || instanceKey == null)
        {
            return;
        }

        if (lastFetchedInstanceKey == instanceKey)
        {
            return;
        }

        if (DateTime.UtcNow < nextFetchAttemptUtc)
        {
            return;
        }

        try
        {
            string url = $"{ApiUrl}?instanceKey={Uri.EscapeDataString(instanceKey)}";
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            HttpResponseMessage response = Http.Send(request);
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                logger.Warn("[PotCycleSync] fetch rejected: {Status}", response.StatusCode);
                nextFetchAttemptUtc = DateTime.UtcNow + FetchRetryDelay;
                return;
            }

            PotCycleApiResponse? parsed = JsonSerializer.Deserialize<PotCycleApiResponse>(body, JsonOptions);
            lastFetchedInstanceKey = instanceKey;
            nextFetchAttemptUtc = DateTime.UtcNow;

            if (parsed is not { Found: true } || parsed.PotFateId == 0 || parsed.SpawnAtUnix <= 0)
            {
                return;
            }

            if (parsed.TerritoryId != 0 && parsed.TerritoryId != territory)
            {
                return;
            }

            DateTimeOffset spawnAt = DateTimeOffset.FromUnixTimeSeconds(parsed.SpawnAtUnix);
            if (potCycles.TryApplyRemoteAnchor(parsed.PotFateId, spawnAt, territory))
            {
                logger.Info(
                    "[PotCycleSync] applied remote pot={PotId} spawn={Spawn}",
                    parsed.PotFateId,
                    parsed.SpawnAtUnix);
            }
        }
        catch (Exception ex)
        {
            logger.Warn("[PotCycleSync] fetch failed: {Message}", ex.Message);
            nextFetchAttemptUtc = DateTime.UtcNow + FetchRetryDelay;
        }
    }

    private void RefreshFingerprint(ushort territory)
    {
        uint? datacenterId = TryGetDatacenterId();
        if (datacenterId is not uint dc)
        {
            return;
        }

        // Keep the established fingerprint while that FATE is still up — using "oldest active
        // FATE" alone rekeys every time a FATE ends and was wiping the next-pot timer.
        if (instanceKey != null
            && fingerprintFateId != 0
            && fates.Snapshot().Any(f =>
                f.Id.Value == fingerprintFateId && f.StartTimeEpoch == fingerprintStartEpoch))
        {
            return;
        }

        Fate? fingerprintFate = fates.Snapshot()
            .Where(f => f.StartTimeEpoch > 0)
            .OrderBy(f => f.StartTimeEpoch)
            .ThenBy(f => f.Id.Value)
            .FirstOrDefault();

        if (fingerprintFate == null)
        {
            return;
        }

        string newKey = ComputeInstanceKey(dc, fingerprintFate.Id.Value, fingerprintFate.StartTimeEpoch);
        if (instanceKey == newKey)
        {
            fingerprintFateId = fingerprintFate.Id.Value;
            fingerprintStartEpoch = fingerprintFate.StartTimeEpoch;
            return;
        }

        bool firstKey = instanceKey == null;
        fingerprintFateId = fingerprintFate.Id.Value;
        fingerprintStartEpoch = fingerprintFate.StartTimeEpoch;
        instanceKey = newKey;
        fingerprintTerritory = territory;
        lastFetchedInstanceKey = null;

        // Do not clear the pot timer on FATE-roster churn. Local/remote anchors re-validate
        // when the next pot is seen; wiping here caused "next pot → unknown".
        logger.Info(
            firstKey
                ? "[PotCycleSync] instance key from fate={FateId} epoch={Epoch} key={KeyPrefix}…"
                : "[PotCycleSync] fingerprint fate ended — new key from fate={FateId} epoch={Epoch} key={KeyPrefix}… (pot timer kept)",
            fingerprintFateId,
            fingerprintStartEpoch,
            instanceKey[..8]);
    }

    private uint? TryGetDatacenterId()
    {
        try
        {
            return player.PlayerCharacter?.CurrentWorld.Value.DataCenter.RowId;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Linker-compatible: SHA-256 hex of three little-endian int32s (dc, fateId, startEpoch).</summary>
    public static string ComputeInstanceKey(uint datacenterId, uint fateId, int startTimeEpoch)
    {
        Span<byte> buffer = stackalloc byte[12];
        BitConverter.TryWriteBytes(buffer[..4], (int)datacenterId);
        BitConverter.TryWriteBytes(buffer[4..8], (int)fateId);
        BitConverter.TryWriteBytes(buffer[8..12], startTimeEpoch);
        return Convert.ToHexString(SHA256.HashData(buffer));
    }

    private string GetInstallationHash()
    {
        string path = Path.Combine(plugin.ConfigDirectory.FullName, "coffer-installation-id.txt");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, Guid.NewGuid().ToString("N"));
        }

        string id = File.ReadAllText(path).Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant();
    }

    private void ResetFingerprint(ushort territory)
    {
        fingerprintTerritory = territory;
        instanceKey = null;
        fingerprintFateId = 0;
        fingerprintStartEpoch = 0;
        lastFetchedInstanceKey = null;
        nextFetchAttemptUtc = DateTime.MinValue;
    }

    private void ResetSession()
    {
        if (fingerprintTerritory == 0 && instanceKey == null)
        {
            return;
        }

        // Drop sync fingerprint only — keep pot timers so "next pot" survives leaving OC / toggling sync.
        fingerprintTerritory = 0;
        instanceKey = null;
        fingerprintFateId = 0;
        fingerprintStartEpoch = 0;
        lastFetchedInstanceKey = null;
        nextFetchAttemptUtc = DateTime.MinValue;
    }

    private sealed class PotCycleApiResponse
    {
        [JsonPropertyName("found")]
        public bool Found { get; set; }

        [JsonPropertyName("territoryId")]
        public int TerritoryId { get; set; }

        [JsonPropertyName("potFateId")]
        public int PotFateId { get; set; }

        [JsonPropertyName("spawnAtUnix")]
        public long SpawnAtUnix { get; set; }
    }
}
