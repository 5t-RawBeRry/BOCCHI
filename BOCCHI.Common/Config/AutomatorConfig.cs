using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 0)]
public class AutomatorConfig : IAutoConfig
{
    [Checkbox] public bool ShouldAutoMount { get; set; } = true;

    /// <summary>
    ///     Mount sheet row ID to use when auto-mounting. 0 = Mount Roulette.
    /// </summary>
    [IntRange(0, 999)]
    public int PreferredMountId { get; set; } = 0;

    [Checkbox] public bool ShouldCastTreasureSight { get; set; } = false;

    [IntRange(60, 600)] public int TreasureSightRecastIntervalSeconds { get; set; } = 120;

    [IntRange(0, 60)] public int MaxRemoteIdleTimeSeconds { get; set; } = 10;
}
