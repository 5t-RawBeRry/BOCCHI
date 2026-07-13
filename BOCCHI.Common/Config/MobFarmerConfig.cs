using BOCCHI.Common.Data.Mobs;
using BOCCHI.Common.Config.Fields;
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

    [IntRange(1, 28)]
    public int MaxMobLevel { get; set; } = 28;

    [FloatRange(10f, 1000f)]
    public float MaxEuclideanDistance { get; set; } = 75f;

    [Checkbox]
    public bool ReturnToStartInWaitingPhase { get; set; } = false;

    [FloatRange(10f, 1000f)]
    public float MinEuclideanDistanceToReturnHome { get; set; } = 200f;

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
