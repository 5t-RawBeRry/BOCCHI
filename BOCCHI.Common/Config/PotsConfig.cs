using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>Pot timing for Illegal Mode and Pots &amp; Treasure.</summary>
[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 1)]
public class PotsConfig : IAutoConfig
{
    /// <summary>
    ///     Skip pot FATEs with less than this many minutes left (0 = disabled).
    /// </summary>
    [IntRange(0, 15, Order = 0, Section = "timing")]
    public int MinPotFateMinutesRemaining { get; set; }

    /// <summary>
    ///     Minutes before predicted pot spawn to leave for pot.
    /// </summary>
    [IntRange(0, 15, Order = 1, Section = "timing")]
    public int PotSpawnLeadMinutes { get; set; } = 3;

    /// <summary>
    ///     Do not start a FATE when pot departure is within this many minutes (0 = disabled).
    /// </summary>
    [IntRange(0, 30, Order = 2, Section = "timing")]
    public int FateFallbackCutoffMinutes { get; set; } = 5;

    /// <summary>
    ///     Do not start a Critical Encounter when pot departure is within this many minutes (0 = disabled).
    /// </summary>
    [IntRange(0, 30, Order = 3, Section = "timing")]
    public int CeFallbackCutoffMinutes { get; set; } = 10;

    [Checkbox(Order = 4, Section = "chests")]
    public bool ShouldFarmRerollPotChests { get; set; } = true;

    /// <summary>
    ///     True when a live pot should not be started / pathing to — already registered pots stay.
    /// </summary>
    /// <summary>
    ///     A FATE that has not started yet reports a nonsense TimeRemaining, so only judge a pot on
    ///     the clock once it is actually running — otherwise a pot is dropped the moment it spawns.
    /// </summary>
    public bool ShouldSkipLivePot(long timeRemainingSeconds) =>
        MinPotFateMinutesRemaining > 0
        && timeRemainingSeconds > 0
        && timeRemainingSeconds < MinPotFateMinutesRemaining * 60L;
}
