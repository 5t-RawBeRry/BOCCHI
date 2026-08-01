using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 1)]
public class FatesConfig : IAutoConfig
{
    [Checkbox]
    public bool ShouldDoFates { get; set; } = true;

    [Checkbox]
    public bool PreferPotFates { get; set; } = false;

    /// <summary>
    ///     Skip / abandon pot FATEs with less than this many minutes left (0 = disabled).
    /// </summary>
    [IntRange(0, 15)]
    public int MinPotFateMinutesRemaining { get; set; } = 2;

    /// <summary>
    ///     Minutes before predicted pot spawn to leave for pot (AOCCH SpawnLeadMinutes).
    /// </summary>
    [IntRange(0, 15)]
    public int PotSpawnLeadMinutes { get; set; } = 3;

    /// <summary>
    ///     Do not start a FATE when pot departure is within this many minutes (0 = disabled).
    /// </summary>
    [IntRange(0, 30)]
    public int FateFallbackCutoffMinutes { get; set; } = 5;

    /// <summary>
    ///     Do not start a CE when pot departure is within this many minutes (0 = disabled).
    /// </summary>
    [IntRange(0, 30)]
    public int CeFallbackCutoffMinutes { get; set; } = 10;

    [Checkbox]
    public bool ShouldFarmPotChests { get; set; } = false;

    [Checkbox]
    public bool ShouldFarmRerollPotChests { get; set; } = true;

    [DisabledFateIds]
    public HashSet<uint> DisabledFateIds { get; set; } =
    [
        // South Horn
        1965, // The Winged Terror
        1976, // Persistent Pots
        1977, // Pleading Pots
        // North Horn
        2072, // Daylight Pottery
        2073 // In a Pot of Bother

    ];

    public bool IsFateEnabled(uint fateId) => ShouldDoFates && !DisabledFateIds.Contains(fateId);
}
