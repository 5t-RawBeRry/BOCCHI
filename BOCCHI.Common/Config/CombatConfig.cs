using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 3)]
public class CombatConfig : IAutoConfig
{
    [Checkbox] public bool ShouldHandleTargeting { get; set; } = false;

    [IntRange(1, 99)] public int AutoRepairThreshold { get; set; } = 30;
}
