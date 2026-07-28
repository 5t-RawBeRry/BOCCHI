using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 2)]
public class BuffConfig : IAutoConfig
{
    [Checkbox] public bool ShouldAutomateBuffs { get; set; } = false;

    [Checkbox] public bool ApplyRomeosBallad { get; set; } = true;

    [Checkbox] public bool ApplyEnduringFortitude { get; set; } = true;

    [Checkbox] public bool ApplyFleetfooted { get; set; } = true;

    [Checkbox] public bool ApplyQuickerStep { get; set; } = false;

    [Checkbox] public bool ApplyBuffsUsingInquiringMind { get; set; } = true;

    [IntRange(0, 25)] public int ReapplyThreshold { get; set; } = 10;

    [FloatRange(30, 300)] public float KnowledgeCrystalDistance { get; set; } = 30f;

    public bool ShouldApplyRomeosBallad() => ShouldAutomateBuffs && ApplyRomeosBallad;

    public bool ShouldApplyEnduringFortitude() => ShouldAutomateBuffs && ApplyEnduringFortitude;

    public bool ShouldApplyFleetfooted() => ShouldAutomateBuffs && ApplyFleetfooted;

    public bool ShouldApplyQuickerStep() => ShouldAutomateBuffs && ApplyQuickerStep;
}
