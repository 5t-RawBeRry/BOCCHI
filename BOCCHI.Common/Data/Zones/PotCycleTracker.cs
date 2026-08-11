using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;

namespace BOCCHI.Common.Data.Zones;

public sealed record PotCycleSnapshot
{
    public ushort TerritoryTypeId { get; init; }

    public bool HasKnownAnchor { get; init; }

    /// <summary>Pot FATE id that established this cycle (local or remote).</summary>
    public int AnchorPotFateId { get; init; }

    /// <summary>Spawn time of <see cref="AnchorPotFateId"/>.</summary>
    public DateTimeOffset AnchorSpawnAt { get; init; }

    /// <summary>True when the anchor came from pot-cycle sync rather than a local sighting.</summary>
    public bool IsRemoteAnchor { get; init; }

    public int CurrentActivePotFateId { get; init; }

    public int PredictedNextPotFateId { get; init; }

    public DateTimeOffset PredictedNextSpawnAt { get; init; }

    public bool HasPredictedNextPot =>
        HasKnownAnchor
        && PredictedNextPotFateId != 0
        && PredictedNextSpawnAt > DateTimeOffset.MinValue;

    public bool HasAnythingToShow =>
        CurrentActivePotFateId != 0 || HasPredictedNextPot;
}

public readonly record struct PotFallbackStartDecision(
    bool AllowStart,
    string Reason,
    DateTimeOffset DepartureAt = default,
    TimeSpan TimeUntilDeparture = default);

public interface IPotCycleTracker
{
    /// <summary>Cycle for the Occult Crescent zone you are in now (empty outside OC).</summary>
    PotCycleSnapshot Snapshot { get; }

    /// <summary>South Horn and/or North Horn cycles that have been seen this session.</summary>
    IReadOnlyList<PotCycleSnapshot> KnownCycles { get; }

    /// <summary>
    ///     Apply a shared pot spawn from another BOCCHI client on the same instance.
    ///     Ignored when a newer local (or equal) anchor already exists, or a pot is live locally.
    /// </summary>
    bool TryApplyRemoteAnchor(int potFateId, DateTimeOffset spawnAt, ushort territoryTypeId);
}

/// <summary>
/// Tracks the 30-minute alternating pot FATE cycle per Occult Crescent zone.
/// Observing one pot predicts the opposite pot's next spawn. SH and NH are kept separately.
/// </summary>
public sealed class PotCycleTracker
(
    IFateRepository fates,
    IZoneProvider zones,
    ILogger<PotCycleTracker> logger
) : IPotCycleTracker, IOnUpdate
{
    private static readonly TimeSpan PotCycleInterval = TimeSpan.FromMinutes(30);

    private static readonly PotCycleSnapshot Empty = new();

    private readonly Dictionary<ushort, PotCycleSnapshot> cycles = new();

    public PotCycleSnapshot Snapshot
    {
        get
        {
            IZone zone = zones.GetZone();
            if (!zone.IsOccultCrescentZone())
            {
                return Empty;
            }

            return cycles.TryGetValue(zone.TerritoryType, out PotCycleSnapshot? snap) ? snap : Empty;
        }
    }

    public IReadOnlyList<PotCycleSnapshot> KnownCycles =>
        cycles.Values
            .Where(c => c.HasAnythingToShow)
            .OrderBy(c => c.TerritoryTypeId)
            .ToList();

    public UpdateLimit UpdateLimit =>
        new()
        {
            Mode = UpdateLimitMode.Milliseconds,
            Limit = 500
        };

    public void Update()
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ActivityData> potFates = zone.GetPotFateData();
        if (potFates.Count == 0)
        {
            return;
        }

        ushort territory = zone.TerritoryType;
        Fate? active = fates.Snapshot().FirstOrDefault(f => potFates.Any(p => p.Id == f.Id.Value));
        PotCycleSnapshot previous = cycles.TryGetValue(territory, out PotCycleSnapshot? existing)
            ? existing
            : Empty;

        cycles[territory] = BuildSnapshot(territory, potFates, active, now, previous);
    }

    public bool TryApplyRemoteAnchor(int potFateId, DateTimeOffset spawnAt, ushort territoryTypeId)
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone() || zone.TerritoryType != territoryTypeId)
        {
            return false;
        }

        List<ActivityData> potFates = zone.GetPotFateData();
        if (potFates.All(p => p.Id != potFateId))
        {
            return false;
        }

        PotCycleSnapshot previous = cycles.TryGetValue(territoryTypeId, out PotCycleSnapshot? existing)
            ? existing
            : Empty;

        if (previous.CurrentActivePotFateId != 0)
        {
            return false;
        }

        if (previous.HasKnownAnchor
            && previous.AnchorSpawnAt != DateTimeOffset.MinValue
            && previous.AnchorSpawnAt >= spawnAt)
        {
            return false;
        }

        ActivityData? opposite = potFates.FirstOrDefault(p => p.Id != potFateId);
        Fate? live = fates.Snapshot().FirstOrDefault(f => f.Id.Value == potFateId);
        DateTimeOffset nextSpawn = spawnAt + PotCycleInterval;

        PotCycleSnapshot applied = new()
        {
            TerritoryTypeId = territoryTypeId,
            HasKnownAnchor = true,
            AnchorPotFateId = potFateId,
            AnchorSpawnAt = spawnAt,
            IsRemoteAnchor = true,
            CurrentActivePotFateId = live != null ? potFateId : 0,
            PredictedNextPotFateId = opposite?.Id ?? 0,
            PredictedNextSpawnAt = opposite == null ? DateTimeOffset.MinValue : nextSpawn,
        };

        cycles[territoryTypeId] = applied;
        logger.Info(
            $"[PotCycleTracker] remote anchor zone={territoryTypeId} pot={potFateId} spawnAt={spawnAt:O} next={opposite?.Id ?? 0} nextSpawnAt={nextSpawn:O}");
        return true;
    }

    private PotCycleSnapshot BuildSnapshot(
        ushort territoryType,
        List<ActivityData> potFates,
        Fate? active,
        DateTimeOffset now,
        PotCycleSnapshot previous)
    {
        if (active == null)
        {
            return previous with
            {
                TerritoryTypeId = territoryType,
                CurrentActivePotFateId = 0,
            };
        }

        int activeId = active.Id.Value;
        if (previous.TerritoryTypeId == territoryType
            && previous.CurrentActivePotFateId == activeId
            && !previous.IsRemoteAnchor)
        {
            return previous;
        }

        DateTimeOffset spawnAt = active.StartTimeEpoch > 0
            ? DateTimeOffset.FromUnixTimeSeconds(active.StartTimeEpoch)
            : now;
        ActivityData? opposite = potFates.FirstOrDefault(p => p.Id != activeId);
        DateTimeOffset nextSpawn = spawnAt + PotCycleInterval;

        logger.Info(
            $"[PotCycleTracker] zone={territoryType} local anchor pot={activeId} next={opposite?.Id ?? 0} nextSpawnAt={(opposite == null ? "none" : nextSpawn.ToString("O"))}");

        return new PotCycleSnapshot
        {
            TerritoryTypeId = territoryType,
            HasKnownAnchor = true,
            AnchorPotFateId = activeId,
            AnchorSpawnAt = spawnAt,
            IsRemoteAnchor = false,
            CurrentActivePotFateId = activeId,
            PredictedNextPotFateId = opposite?.Id ?? 0,
            PredictedNextSpawnAt = opposite == null ? DateTimeOffset.MinValue : nextSpawn,
        };
    }
}

public static class PotFallbackWindow
{
    public static PotFallbackStartDecision Evaluate(
        PotCycleSnapshot cycle,
        DateTimeOffset now,
        TimeSpan cutoffWindow,
        int spawnLeadMinutes,
        bool potFarmingEnabled,
        string activityName)
    {
        if (!potFarmingEnabled)
        {
            return new(true, $"{activityName} allowed: pot farming disabled.");
        }

        if (!cycle.HasPredictedNextPot)
        {
            return new(true, $"{activityName} allowed: no pot departure predicted yet.");
        }

        DateTimeOffset departureAt = cycle.PredictedNextSpawnAt - TimeSpan.FromMinutes(Math.Max(0, spawnLeadMinutes));
        TimeSpan timeUntilDeparture = departureAt - now;
        if (timeUntilDeparture <= cutoffWindow)
        {
            return new(
                false,
                $"{activityName} blocked: pot departure in {Format(timeUntilDeparture)} (cutoff {cutoffWindow.TotalMinutes:0}m).",
                departureAt,
                timeUntilDeparture);
        }

        return new(
            true,
            $"{activityName} allowed: pot departure in {Format(timeUntilDeparture)}.",
            departureAt,
            timeUntilDeparture);
    }

    public static bool ShouldPreposition(
        PotCycleSnapshot cycle,
        DateTimeOffset now,
        TimeSpan cutoffWindow,
        int spawnLeadMinutes,
        bool potFarmingEnabled)
    {
        if (!potFarmingEnabled || !cycle.HasPredictedNextPot)
        {
            return false;
        }

        PotFallbackStartDecision decision = Evaluate(
            cycle,
            now,
            cutoffWindow,
            spawnLeadMinutes,
            potFarmingEnabled,
            "preposition");

        return !decision.AllowStart;
    }

    private static string Format(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "0m";
        }

        return value.TotalMinutes >= 1
            ? $"{Math.Floor(value.TotalMinutes):0}m"
            : $"{Math.Ceiling(value.TotalSeconds):0}s";
    }
}
