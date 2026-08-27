using Ocelot.Config;
using Ocelot.Config.Fields;
using BOCCHI.Common.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("shopping", GroupOrder = 25)]
public class ShoppingConfig : IAutoConfig
{
    /// <summary>Drawn by <see cref="ShopShoppingListAttribute"/> page UI.</summary>
    [ConfigHidden]
    public bool EnableAutoShop { get; set; } = false;

    /// <summary>Drawn by shopping page UI. 0 = never start from silver.</summary>
    [ConfigHidden]
    public int SilverThreshold { get; set; } = 8000;

    /// <summary>Drawn by shopping page UI. 0 = never start from gold.</summary>
    [ConfigHidden]
    public int GoldThreshold { get; set; } = 0;

    [ConfigHidden]
    public int ReserveSilver { get; set; } = 0;

    [ConfigHidden]
    public int ReserveGold { get; set; } = 0;

    /// <summary>
    /// Full Shopping page (enable, thresholds, ICE-style Keep / Buy / Sink list).
    /// Also edits <see cref="Shopping"/>.
    /// </summary>
    [ShopShoppingList(Order = 0)]
    public List<uint> ShoppingOrder { get; set; } = [];

    public Dictionary<uint, ShopListEntry> Shopping { get; set; } = new();

    /// <summary>Legacy checkbox picks — migrated into <see cref="Shopping"/> on load.</summary>
    public HashSet<uint> PreferredItemIds { get; set; } = [];

    public bool HasShoppingList => ShoppingOrder.Count > 0 && Shopping.Count > 0;
}
