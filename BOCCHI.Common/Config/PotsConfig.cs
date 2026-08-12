using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>Pot timing for Illegal Mode and Pots &amp; Treasure.</summary>
[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 1)]
public class PotsConfig : IAutoConfig
{
    /// <summary>
    ///     Skip pot FATEs at or above this progress % (0 = disabled). Preferred over minutes (#174).
    /// </summary>
    [IntRange(0, 100, Order = 0, Section = "timing")]
    public int MaxPotFateProgressPercent { get; set; } = 50;

    /// <summary>
    ///     Skip pot FATEs with less than this many minutes left (0 = disabled).
    /// </summary>
    [IntRange(0, 15, Order = 1, Section = "timing")]
    public int MinPotFateMinutesRemaining { get; set; }

    /// <summary>
    ///     Minutes before predicted pot spawn to leave for pot.
    /// </summary>
    [IntRange(0, 15, Order = 2, Section = "timing")]
    public int PotSpawnLeadMinutes { get; set; } = 3;

    /// <summary>
    ///     Do not start a FATE when pot departure is within this many minutes (0 = disabled).
    /// </summary>
    [IntRange(0, 30, Order = 3, Section = "timing")]
    public int FateFallbackCutoffMinutes { get; set; } = 5;

    /// <summary>
    ///     Do not start a Critical Encounter when pot departure is within this many minutes (0 = disabled).
    /// </summary>
    [IntRange(0, 30, Order = 4, Section = "timing")]
    public int CeFallbackCutoffMinutes { get; set; } = 10;

    [Checkbox(Order = 5, Section = "chests")]
    public bool ShouldFarmRerollPotChests { get; set; } = true;

    /// <summary>
    ///     True when a live pot should not be started / pathing to — already registered pots stay.
    /// </summary>
    public bool ShouldSkipLivePot(byte progress, long timeRemainingSeconds)
    {
        if (MaxPotFateProgressPercent > 0 && progress >= MaxPotFateProgressPercent)
        {
            return true;
        }

        if (MinPotFateMinutesRemaining > 0
            && timeRemainingSeconds < MinPotFateMinutesRemaining * 60L)
        {
            return true;
        }

        return false;
    }
}
