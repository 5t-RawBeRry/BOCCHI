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
    public bool DrawLineToCarrots { get; set; } = true;

    [Checkbox]
    public bool ShowPercentageActiveTreasureCount { get; set; } = false;

    [Checkbox]
    public bool EnableTreasureHunt { get; set; } = false;

    /// <summary>Cast Return to base camp after the last coffer on the hunt route.</summary>
    [Checkbox]
    public bool ReturnToBaseCampAfterHunt { get; set; } = true;

    /// <summary>Play a chat sound effect when the hunt finishes (#120).</summary>
    [Checkbox]
    public bool PlaySoundOnHuntComplete { get; set; } = true;

    /// <summary>Chat SFX 1–16 (same IDs as System Config → Sound Effects).</summary>
    [IntRange(1, 16)]
    public int HuntCompleteSoundId { get; set; } = 2;

    [FloatRange(50f, 500f)]
    public float HuntReturnCost { get; set; } = 300f;

    [FloatRange(10f, 500f)]
    public float HuntTeleportCost { get; set; } = 50f;

    [FloatRange(10f, 100f)]
    public float HuntDetectionRange { get; set; } = 75f;

    [IntRange(1, 50)]
    public int HuntMaxLevel { get; set; } = 40;

    /// <summary>
    ///     Only interact with known coffer BaseIds (2014741–43). Off = any Treasure object.
    /// </summary>
    [Checkbox]
    public bool RestrictCofferBaseIds { get; set; } = true;

    /// <summary>
    ///     Pause treasure hunting during Ashkin / unsafe weather windows (South Horn).
    /// </summary>
    [Checkbox]
    public bool SkipUnsafeTreasureWindows { get; set; } = true;
}
