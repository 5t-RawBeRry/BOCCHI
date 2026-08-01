namespace BOCCHI.Common.Data.Shopping;

/// <summary>Known Antiquarian currency-shop rows (AOCCH catalog subset).</summary>
public readonly record struct ShopCatalogEntry(
    uint ItemId,
    string Name,
    uint Cost,
    uint RowIndex,
    int MenuIndex,
    int TabId,
    uint CurrencyItemId);

public static class ShopCatalog
{
    public const uint SilverPieceItemId = 45043;
    public const uint GoldPieceItemId = 45044;

    /// <summary>South Horn silver armor tab (IL 745) — common spend targets.</summary>
    public static readonly ShopCatalogEntry[] SilverArmor =
    [
        new(47758, "Arcanaut's Pelt of Fending", 4000, 0, 0, 1, SilverPieceItemId),
        new(47773, "Arcanaut's Pelt of Maiming", 4000, 1, 0, 1, SilverPieceItemId),
        new(47788, "Arcanaut's Bicorne of Striking", 4000, 2, 0, 1, SilverPieceItemId),
        new(47818, "Arcanaut's Bicorne of Scouting", 4000, 3, 0, 1, SilverPieceItemId),
        new(47803, "Arcanaut's Bicorne of Aiming", 4000, 4, 0, 1, SilverPieceItemId),
        new(47848, "Arcanaut's Sugarloaf Hat of Casting", 4000, 5, 0, 1, SilverPieceItemId),
        new(47833, "Arcanaut's Sugarloaf Hat of Healing", 4000, 6, 0, 1, SilverPieceItemId),
    ];

    public static IEnumerable<ShopCatalogEntry> All => SilverArmor;

    public static bool TryGet(uint itemId, out ShopCatalogEntry entry)
    {
        foreach (ShopCatalogEntry candidate in All)
        {
            if (candidate.ItemId != itemId)
            {
                continue;
            }

            entry = candidate;
            return true;
        }

        entry = default;
        return false;
    }
}
