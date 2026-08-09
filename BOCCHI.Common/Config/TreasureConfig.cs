using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("treasure", GroupOrder = 20)]
public class TreasureConfig : IAutoConfig
{
    [Checkbox(Order = 0, Section = "radar")]
    public bool DrawLineToBronzeChests { get; set; } = true;

    [Checkbox(Order = 1, Section = "radar")]
    public bool DrawLineToSilverChests { get; set; } = true;

    [Checkbox(Order = 2, Section = "radar")]
    public bool DrawLineToCarrots { get; set; } = true;

    [Checkbox(Order = 3, Section = "radar")]
    public bool ShowPercentageActiveTreasureCount { get; set; } = false;

    /// <summary>Cast Return to base camp after the last coffer on the hunt route.</summary>
    [Checkbox(Order = 4, Section = "completion")]
    public bool ReturnToBaseCampAfterHunt { get; set; } = true;

    /// <summary>Play an MP3 when the hunt finishes.</summary>
    [Checkbox(Order = 5, Section = "completion")]
    public bool PlaySoundOnHuntComplete { get; set; } = true;

    /// <summary>MP3 name (without extension) from the plugin Sounds folder. Default Moogle.</summary>
    [Mp3SoundSelect(Order = 6, Section = "completion")]
    public string HuntCompleteSound { get; set; } = "Moogle";

    /// <summary>Cast Treasure Sight at hunt start and every N coffers; abort early when Sight reports none left.</summary>
    [Checkbox(Order = 7, Section = "hunt")]
    public bool CastTreasureSightDuringHunt { get; set; } = true;

    /// <summary>Recast Treasure Sight every N coffers after the opening cast.</summary>
    [IntRange(1, 50, Order = 8, Section = "hunt")]
    public int TreasureSightEveryNLocations { get; set; } = 10;

    [IntRange(1, 50, Order = 9, Section = "hunt")]
    public int HuntMaxLevel { get; set; } = 50;

    /// <summary>Pause treasure hunting during Ashkin / unsafe weather windows (South Horn).</summary>
    [Checkbox(Order = 10, Section = "hunt")]
    public bool SkipUnsafeTreasureWindows { get; set; } = true;

    /// <summary>Illegal Mode / Completionist: after CE/FATE, Sight (if known) then hunt, or map hunt without Sight.</summary>
    [Checkbox(Order = 11, Section = "hunt")]
    public bool EnableAutomaticTreasureHuntDuringIllegalMode { get; set; } = false;

    /// <summary>Use real Ninja Hide near high-knowledge hostiles while hunting coffers.</summary>
    [Checkbox(Order = 12, Section = "ninja_hide")]
    public bool UseNinjaHideOnDangerousRoutes { get; set; } = false;

    /// <summary>Gearset number (1-based) that equips Ninja. 0 = already on Ninja only.</summary>
    [IntRange(0, 100, Order = 13, Section = "ninja_hide")]
    public int NinjaGearsetNumber { get; set; } = 0;

    /// <summary>Hide when mob knowledge ≥ player knowledge + this offset.</summary>
    [IntRange(-5, 10, Order = 14, Section = "ninja_hide")]
    public int KnowledgeHideOffset { get; set; } = 0;

    /// <summary>Start Hide when a knowledge threat is within this distance (yalms).</summary>
    [FloatRange(5f, 40f, Order = 15, Section = "ninja_hide")]
    public float KnowledgeThreatEnterDistance { get; set; } = 10f;

    /// <summary>Clear Hide requirement when threats are beyond this distance (yalms).</summary>
    [FloatRange(10f, 60f, Order = 16, Section = "ninja_hide")]
    public float KnowledgeThreatExitDistance { get; set; } = 20f;
}
