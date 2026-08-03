using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("forked_tower", GroupOrder = 50)]
public class ForkedTowerConfig : IAutoConfig
{
    /// <summary>
    ///     South Horn Forked Tower only for now (#116). North Horn later.
    /// </summary>
    [Checkbox]
    public bool Enabled { get; set; } = true;

    [Checkbox]
    public bool DrawPotentialTrapPositions { get; set; } = true;

    [Checkbox]
    public bool ShowRegistrationCountdown { get; set; } = true;

    [FloatRange(20f, 300f)]
    public float TrapDrawRange { get; set; } = 150f;
}
