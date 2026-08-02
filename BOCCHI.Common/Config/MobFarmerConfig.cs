using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Mobs;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("mob_farmer", GroupOrder = 20)]
public class MobFarmerConfig : IAutoConfig
{
    [MobMultiSelect]
    public List<Mob> Mobs { get; set; } = [];

    [Checkbox]
    public bool ConsiderSpecialMobs { get; set; } = false;

    [IntRange(1, 50)]
    public int MaxMobLevel { get; set; } = 40;

    [FloatRange(10f, 1000f)]
    public float MaxEuclideanDistance { get; set; } = 75f;

    [Checkbox]
    public bool ReturnToStartInWaitingPhase { get; set; } = false;

    [FloatRange(10f, 1000f)]
    public float MinEuclideanDistanceToReturnHome { get; set; } = 200f;

    /// <summary>
    ///     When off, weather/time specials still pull (if enabled) but do not count toward min pack thresholds.
    /// </summary>
    [Checkbox]
    public bool CountSpecialMobsTowardMinimum { get; set; } = false;

    /// <summary>
    ///     Do not begin a new Buffing → Gathering loop while the InCombat flag is still set.
    /// </summary>
    [Checkbox]
    public bool OnlyStartOutOfCombat { get; set; } = false;

    [Checkbox]
    public bool RenderDebugLines { get; set; } = false;

    [Checkbox]
    public bool RenderDebugLinesWhileNotRunning { get; set; } = false;

    [Checkbox]
    public bool ApplyBattleBell { get; set; } = false;

    [FloatRange(0f, 30f)]
    public float MaximumBattleBellWaitTime { get; set; } = 10f;

    [IntRange(0, 20)]
    public int MinimumMobsToStartLoop { get; set; } = 0;

    [IntRange(1, 20)]
    public int MinimumMobsToStartFight { get; set; } = 5;

    [FloatRange(5f, 60f)]
    public float StackingTimeoutSeconds { get; set; } = 15f;
}
