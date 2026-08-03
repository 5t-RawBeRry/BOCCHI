using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;

namespace BOCCHI.Common.Data.Zones;

public sealed record PotCycleSnapshot
{
    public DateTimeOffset LastUpdated { get; init; }

    public ushort TerritoryTypeId { get; init; }

    public bool HasKnownAnchor { get; init; }

    public int CurrentActivePotFateId { get; init; }

    public int PredictedNextPotFateId { get; init; }

    public DateTimeOffset PredictedNextSpawnAt { get; init; }

    public bool HasPredictedNextPot =>
        HasKnownAnchor
        && PredictedNextPotFateId != 0
        && PredictedNextSpawnAt > DateTimeOffset.MinValue;
}

public readonly record struct PotFallbackStartDecision(
    bool AllowStart,
    string Reason,
    DateTimeOffset DepartureAt = default,
    TimeSpan TimeUntilDeparture = default);

public interface IPotCycleTracker
{
    PotCycleSnapshot Snapshot { get; }
}

/// <summary>
/// Tracks the 30-minute alternating pot FATE cycle (AOCCH algorithm).
/// Observing one pot predicts the opposite pot's next spawn.
/// </summary>
public sealed class PotCycleTracker
(
    IFateRepository fates,
    IZoneProvider zones,
    ILogger<PotCycleTracker> logger
) : IPotCycleTracker, IOnUpdate
{
    private static readonly TimeSpan PotCycleInterval = TimeSpan.FromMinutes(30);

    private PotCycleSnapshot snapshot = new();

    public PotCycleSnapshot Snapshot => snapshot;

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

        Fate? active = fates.Snapshot().FirstOrDefault(f => potFates.Any(p => p.Id == f.Id.Value));
        snapshot = BuildSnapshot(zone.TerritoryType, potFates, active, now, snapshot);
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
            bool sameTerritory = previous.TerritoryTypeId == territoryType;
            return previous with
            {
                LastUpdated = now,
                TerritoryTypeId = territoryType,
                HasKnownAnchor = sameTerritory && previous.HasKnownAnchor,
                CurrentActivePotFateId = 0,
                PredictedNextPotFateId = sameTerritory ? previous.PredictedNextPotFateId : 0,
                PredictedNextSpawnAt = sameTerritory ? previous.PredictedNextSpawnAt : DateTimeOffset.MinValue
            };
        }

        int activeId = active.Id.Value;
        if (previous.TerritoryTypeId == territoryType && previous.CurrentActivePotFateId == activeId)
        {
            return previous with { LastUpdated = now };
        }

        ActivityData? opposite = potFates.FirstOrDefault(p => p.Id != activeId);
        logger.Info(
            $"[PotCycleTracker] anchor pot={activeId} next={opposite?.Id ?? 0} nextSpawnAt={(opposite == null ? "none" : (now + PotCycleInterval).ToString("O"))}");

        return new PotCycleSnapshot
        {
            LastUpdated = now,
            TerritoryTypeId = territoryType,
            HasKnownAnchor = true,
            CurrentActivePotFateId = activeId,
            PredictedNextPotFateId = opposite?.Id ?? 0,
            PredictedNextSpawnAt = opposite == null ? DateTimeOffset.MinValue : now + PotCycleInterval
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
