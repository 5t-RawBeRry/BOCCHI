using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("treasure", GroupOrder = 40)]
public class TreasureConfig : IAutoConfig
{
    [Checkbox]
    public bool Enabled { get; set; } = true;

    [Checkbox]
    public bool DrawLineToBronzeChests { get; set; } = true;

    [Checkbox]
    public bool DrawLineToSilverChests { get; set; } = true;

    [Checkbox]
    public bool ShowPercentageActiveTreasureCount { get; set; } = false;

    [Checkbox]
    public bool EnableTreasureHunt { get; set; } = false;
    [FloatRange(50f, 500f)]
    public float HuntReturnCost { get; set; } = 300f;

    [FloatRange(10f, 500f)]
    public float HuntTeleportCost { get; set; } = 50f;

    [FloatRange(10f, 100f)]
    public float HuntDetectionRange { get; set; } = 75f;

    [IntRange(1, 28)]
    public int HuntMaxLevel { get; set; } = 23;
}
