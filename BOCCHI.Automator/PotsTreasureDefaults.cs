namespace BOCCHI.Automator.Services;

/// <summary>Fallback timing for Pots &amp; Treasure when config is unavailable.</summary>
public static class PotsTreasureDefaults
{
    /// <summary>Default leave-hunt lead before predicted pot spawn (matches <see cref="Common.Config.PotsConfig.PotSpawnLeadMinutes"/> default).</summary>
    public const int PrepositionLeadMinutes = 3;

    /// <summary>Skip / abandon pot FATEs with less than this many minutes left.</summary>
    public const int MinPotFateMinutesRemaining = 2;
}
