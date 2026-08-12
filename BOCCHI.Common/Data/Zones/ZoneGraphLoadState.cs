namespace BOCCHI.Common.Data.Zones;

/// <summary>Where the current zone path map came from (player-facing status).</summary>
public enum ZoneGraphSource
{
    None = 0,
    Cache = 1,
    Shipped = 2,
    Built = 3,
}

/// <summary>Lifecycle of the zone path map used by Illegal Mode routing.</summary>
public enum ZoneGraphLoadState
{
    /// <summary>Not loaded yet (or not in Occult Crescent).</summary>
    Idle = 0,

    /// <summary>Reading disk cache or bundled map.</summary>
    Loading = 1,

    /// <summary>Building walk costs with vnav (can take a while; automation waits).</summary>
    Building = 2,

    /// <summary>Ready for route planning.</summary>
    Ready = 3,
}
