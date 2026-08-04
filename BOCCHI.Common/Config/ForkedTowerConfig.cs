using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("forked_tower", GroupOrder = 50)]
public class ForkedTowerConfig : IAutoConfig
{
    [Checkbox(Order = 0)]
    public bool DrawPotentialTrapPositions { get; set; } = true;

    [Checkbox(Order = 1)]
    public bool ShowRegistrationCountdown { get; set; } = true;

    [FloatRange(20f, 300f, Order = 2)]
    public float TrapDrawRange { get; set; } = 150f;
}
