using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>Hidden for now — shopping automation is not ready for release.</summary>
[Serializable]
[ConfigHidden]
public class ShoppingConfig : IAutoConfig
{
    [Checkbox]
    public bool EnableAutoShop { get; set; } = false;

    /// <summary>Start shopping when silver pieces reach this (0 = never trigger on silver).</summary>
    [IntRange(0, 50000)]
    public int SilverThreshold { get; set; } = 16000;

    /// <summary>Start shopping when gold pieces reach this (0 = never trigger on gold).</summary>
    [IntRange(0, 50000)]
    public int GoldThreshold { get; set; } = 0;

    [IntRange(0, 50000)]
    public int ReserveSilver { get; set; } = 0;

    [IntRange(0, 50000)]
    public int ReserveGold { get; set; } = 0;

    /// <summary>
    ///     Item IDs to buy from the Antiquarian currency shop (empty = do not auto-buy;
    ///     approach/open shop only when thresholds hit).
    /// </summary>
    public HashSet<uint> PreferredItemIds { get; set; } = [];
}
