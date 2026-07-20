using Ocelot.Config;
using Ocelot.Config.Fields;
namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 0)]
public class AutomatorConfig : IAutoConfig
{
    [Checkbox] public bool ShouldCastTreasureSight { get; set; } = false;

    [IntRange(60, 600)] public int TreasureSightRecastIntervalSeconds { get; set; } = 120;

    [IntRange(0, 60)] public int MaxRemoteIdleTimeSeconds { get; set; } = 10;
}
