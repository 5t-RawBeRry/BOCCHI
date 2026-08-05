using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("treasure", GroupOrder = 20)]
public class TreasureConfig : IAutoConfig
{
    [Checkbox(Order = 0)]
    public bool DrawLineToBronzeChests { get; set; } = true;

    [Checkbox(Order = 1)]
    public bool DrawLineToSilverChests { get; set; } = true;

    [Checkbox(Order = 2)]
    public bool DrawLineToCarrots { get; set; } = true;

    [Checkbox(Order = 3)]
    public bool ShowPercentageActiveTreasureCount { get; set; } = false;

    /// <summary>Cast Return to base camp after the last coffer on the hunt route.</summary>
    [Checkbox(Order = 4)]
    public bool ReturnToBaseCampAfterHunt { get; set; } = true;

    /// <summary>Play an MP3 when the hunt finishes (#120).</summary>
    [Checkbox(Order = 5)]
    public bool PlaySoundOnHuntComplete { get; set; } = true;

    /// <summary>MP3 name (without extension) from the plugin Sounds folder. Default Moogle.</summary>
    [Mp3SoundSelect(Order = 6)]
    public string HuntCompleteSound { get; set; } = "Moogle";

    /// <summary>Cast Treasure Sight at hunt start and every N coffers; abort early when Sight reports none left (#120).</summary>
    [Checkbox(Order = 7)]
    public bool CastTreasureSightDuringHunt { get; set; } = true;

    /// <summary>Recast Treasure Sight every N coffer stops after the opening cast.</summary>
    [IntRange(1, 50, Order = 8)]
    public int TreasureSightEveryNLocations { get; set; } = 10;

    [FloatRange(50f, 500f, Order = 9)]
    public float HuntReturnCost { get; set; } = 300f;

    [FloatRange(10f, 500f, Order = 10)]
    public float HuntTeleportCost { get; set; } = 50f;

    [FloatRange(10f, 100f, Order = 11)]
    public float HuntDetectionRange { get; set; } = 75f;

    [IntRange(1, 50, Order = 12)]
    public int HuntMaxLevel { get; set; } = 40;

    /// <summary>
    ///     Pause treasure hunting during Ashkin / unsafe weather windows (South Horn).
    /// </summary>
    [Checkbox(Order = 13)]
    public bool SkipUnsafeTreasureWindows { get; set; } = true;

    /// <summary>
    ///     Illegal Mode: after CE/FATE, Return to camp, use Treasure Sight when available, then start a treasure hunt.
    /// </summary>
    [Checkbox(Order = 14)]
    public bool EnableAutomaticTreasureHuntDuringIllegalMode { get; set; } = false;

    /// <summary>
    ///     Opt in to anonymously send coffer opens for live hunt routes (authored map when catalog empty).
    /// </summary>
    [Checkbox(Order = 15)]
    public bool EnableCofferObservationSubmission { get; set; } = false;
}
