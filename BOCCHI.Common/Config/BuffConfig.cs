using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 4)]
public class BuffConfig : IAutoConfig
{
    [Checkbox] public bool ShouldAutomateBuffs { get; set; } = false;

    [Checkbox] public bool ApplyRomeosBallad { get; set; } = true;

    [Checkbox] public bool ApplyEnduringFortitude { get; set; } = true;

    [Checkbox] public bool ApplyFleetfooted { get; set; } = true;

    [Checkbox] public bool ApplyQuickerStep { get; set; } = false;

    [Checkbox] public bool ApplyBuffsUsingInquiringMind { get; set; } = true;

    /// <summary>Reapply when remaining buff duration is at or below this many minutes.</summary>
    [IntRange(0, 25)] public int ReapplyThreshold { get; set; } = 10;

    [FloatRange(30, 300)] public float KnowledgeCrystalDistance { get; set; } = 30f;

    public bool ShouldApplyRomeosBallad() => ApplyRomeosBallad;

    public bool ShouldApplyEnduringFortitude() => ApplyEnduringFortitude;

    public bool ShouldApplyFleetfooted() => ApplyFleetfooted;

    public bool ShouldApplyQuickerStep() => ApplyQuickerStep;
}
