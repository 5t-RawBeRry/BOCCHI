using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 0)]
public class AutomatorConfig : IAutoConfig
{
    [Checkbox] public bool ShouldAutoMount { get; set; } = true;

    /// <summary>
    ///     Preferred mount sheet row ID. 0 = Mount Roulette.
    /// </summary>
    [MountSelect]
    public uint PreferredMountId { get; set; } = 0;

    /// <summary>
    ///     Toggle <c>BOCCHI AI</c> off while traveling, on at FATE/CE.
    /// </summary>
    [Checkbox] public bool ToggleAiProvider { get; set; } = true;

    /// <summary>
    ///     After Return / aetheryte teleport toward a FATE or CE, stop and leave the walk for the player.
    /// </summary>
    [Checkbox] public bool StopAfterActivityAetheryte { get; set; } = false;

    /// <summary>
    ///     When the current phantom job is maxed, switch to the next unlocked non-maxed job.
    /// </summary>
    [Checkbox] public bool PhantomJobsLevelingMode { get; set; } = false;

    [Checkbox] public bool ShouldCastTreasureSight { get; set; } = false;

    [IntRange(60, 600)] public int TreasureSightRecastIntervalSeconds { get; set; } = 120;

    /// <summary>
    ///     Upper bound (seconds) for the random 2..max wait before Return after a FATE/CE.
    /// </summary>
    [IntRange(2, 60)] public int MaxRemoteIdleTimeSeconds { get; set; } = 10;
}
