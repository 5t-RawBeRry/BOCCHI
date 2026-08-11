namespace BOCCHI.Automator.Services;

/// <summary>Fixed timing for Pots &amp; Treasure (no user config).</summary>
public static class PotsTreasureDefaults
{
    /// <summary>Leave hunt and preposition this many minutes before predicted pot spawn.</summary>
    public const int PrepositionLeadMinutes = 3;

    /// <summary>Skip / abandon pot FATEs with less than this many minutes left.</summary>
    public const int MinPotFateMinutesRemaining = 2;
}
