using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Mobs;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("mob_farmer", GroupOrder = 19)]
public class MobFarmerConfig : IAutoConfig
{
    [MobMultiSelect(Order = 0, Section = "targets")]
    public List<Mob> Mobs { get; set; } = [];

    [Checkbox(Order = 1, Section = "targets")]
    public bool ShouldHandleTargeting { get; set; } = true;

    [Checkbox(Order = 2, Section = "targets")]
    public bool ForceTargetCentralEnemy { get; set; } = true;

    [Checkbox(Order = 3, Section = "targets")]
    public bool ConsiderSpecialMobs { get; set; } = false;

    [IntRange(1, 50, Order = 4, Section = "targets")]
    public int MaxMobLevel { get; set; } = 40;

    [FloatRange(10f, 1000f, Order = 5, Section = "targets")]
    public float MaxEuclideanDistance { get; set; } = 75f;

    [Checkbox(Order = 6, Section = "pulls")]
    public bool CountSpecialMobsTowardMinimum { get; set; } = false;

    [Checkbox(Order = 7, Section = "pulls")]
    public bool OnlyStartOutOfCombat { get; set; } = false;

    [IntRange(0, 20, Order = 8, Section = "pulls")]
    public int MinimumMobsToStartLoop { get; set; } = 0;

    [IntRange(1, 20, Order = 9, Section = "pulls")]
    public int MinimumMobsToStartFight { get; set; } = 5;

    [FloatRange(5f, 60f, Order = 10, Section = "pulls")]
    public float StackingTimeoutSeconds { get; set; } = 15f;

    [Checkbox(Order = 11, Section = "home")]
    public bool ReturnToStartInWaitingPhase { get; set; } = false;

    [FloatRange(10f, 1000f, Order = 12, Section = "home")]
    public float MinEuclideanDistanceToReturnHome { get; set; } = 200f;

    [Checkbox(Order = 13, Section = "buffs")]
    public bool ApplyBattleBell { get; set; } = false;

    [FloatRange(0f, 30f, Order = 14, Section = "buffs")]
    public float MaximumBattleBellWaitTime { get; set; } = 10f;

    [Checkbox(Order = 15, Section = "debug")]
    public bool RenderDebugLines { get; set; } = false;
}
