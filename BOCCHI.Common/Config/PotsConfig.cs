using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>Illegal Mode pot timing (Pots &amp; Treasure uses fixed built-in timing).</summary>
[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 1)]
public class PotsConfig : IAutoConfig
{
    /// <summary>
    ///     Skip / abandon pot FATEs with less than this many minutes left (0 = disabled).
    /// </summary>
    [IntRange(0, 15, Order = 0)]
    public int MinPotFateMinutesRemaining { get; set; } = 2;

    /// <summary>
    ///     Minutes before predicted pot spawn to leave for pot.
    /// </summary>
    [IntRange(0, 15, Order = 1)]
    public int PotSpawnLeadMinutes { get; set; } = 3;

    /// <summary>
    ///     Do not start a FATE when pot departure is within this many minutes (0 = disabled).
    /// </summary>
    [IntRange(0, 30, Order = 2)]
    public int FateFallbackCutoffMinutes { get; set; } = 5;

    /// <summary>
    ///     Do not start a Critical Encounter when pot departure is within this many minutes (0 = disabled).
    /// </summary>
    [IntRange(0, 30, Order = 3)]
    public int CeFallbackCutoffMinutes { get; set; } = 10;

    [Checkbox(Order = 4)]
    public bool ShouldFarmRerollPotChests { get; set; } = true;
}
