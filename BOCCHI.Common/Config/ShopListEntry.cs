namespace BOCCHI.Common.Config;

/// <summary>
/// Per-item shopping goals, matching ICE cosmocredit shopping:
/// Keep = stock target, Buy = one-shot remaining, KeepBuying = currency sink (one item only).
/// </summary>
[Serializable]
public class ShopListEntry
{
    /// <summary>Buy until inventory has at least this many (does not decrease between trips).</summary>
    public int KeepAmount { get; set; }

    /// <summary>Buy this many more times, then stop (decrements after each purchase).</summary>
    public int BuyAmount { get; set; }

    /// <summary>After Keep/Buy are satisfied, keep spending on this item (only one list entry may be true).</summary>
    public bool KeepBuying { get; set; }
}
