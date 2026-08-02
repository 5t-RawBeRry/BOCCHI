using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 4)]
public class CombatConfig : IAutoConfig
{
    [Checkbox(Order = 0)]
    public bool ShouldHandleTargeting { get; set; } = true;

    [Checkbox(Order = 1)]
    public bool ForceTargetCentralEnemy { get; set; } = true;

    [IntRange(1, 99)]
    public int AutoRepairThreshold { get; set; } = 30;
}
