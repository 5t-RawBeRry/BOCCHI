namespace BOCCHI.Common.Data.Zones;

public sealed record PotCycleSnapshot
{
    public DateTimeOffset LastUpdated { get; init; }

    public ushort TerritoryTypeId { get; init; }

    public bool HasKnownAnchor { get; init; }

    public int LastObservedPotFateId { get; init; }

    public DateTimeOffset LastObservedSpawnAt { get; init; }

    public int CurrentActivePotFateId { get; init; }

    public int PredictedNextPotFateId { get; init; }

    public string PredictedNextPotFateName { get; init; } = string.Empty;

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
