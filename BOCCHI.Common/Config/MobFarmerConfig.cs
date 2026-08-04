using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Mobs;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("mob_farmer", GroupOrder = 20)]
public class MobFarmerConfig : IAutoConfig
{
    [MobMultiSelect(Order = 0)]
    public List<Mob> Mobs { get; set; } = [];

    [Checkbox(Order = 1)]
    public bool ShouldHandleTargeting { get; set; } = true;

    [Checkbox(Order = 2)]
    public bool ForceTargetCentralEnemy { get; set; } = true;

    [Checkbox(Order = 3)]
    public bool ConsiderSpecialMobs { get; set; } = false;

    [IntRange(1, 50, Order = 4)]
    public int MaxMobLevel { get; set; } = 40;

    [FloatRange(10f, 1000f, Order = 5)]
    public float MaxEuclideanDistance { get; set; } = 75f;

    [Checkbox(Order = 6)]
    public bool ReturnToStartInWaitingPhase { get; set; } = false;

    [FloatRange(10f, 1000f, Order = 7)]
    public float MinEuclideanDistanceToReturnHome { get; set; } = 200f;

    /// <summary>
    ///     When off, weather/time specials still pull (if enabled) but do not count toward min pack thresholds.
    /// </summary>
    [Checkbox(Order = 8)]
    public bool CountSpecialMobsTowardMinimum { get; set; } = false;

    /// <summary>
    ///     Do not begin a new Buffing → Gathering loop while the InCombat flag is still set.
    /// </summary>
    [Checkbox(Order = 9)]
    public bool OnlyStartOutOfCombat { get; set; } = false;

    [Checkbox(Order = 10)]
    public bool RenderDebugLines { get; set; } = false;

    [Checkbox(Order = 11)]
    public bool ApplyBattleBell { get; set; } = false;

    [FloatRange(0f, 30f, Order = 12)]
    public float MaximumBattleBellWaitTime { get; set; } = 10f;

    [IntRange(0, 20, Order = 13)]
    public int MinimumMobsToStartLoop { get; set; } = 0;

    [IntRange(1, 20, Order = 14)]
    public int MinimumMobsToStartFight { get; set; } = 5;

    [FloatRange(5f, 60f, Order = 15)]
    public float StackingTimeoutSeconds { get; set; } = 15f;
}
