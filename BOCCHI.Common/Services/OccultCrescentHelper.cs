using BOCCHI.Common.Data.Shopping;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace BOCCHI.Common.Services;

public static unsafe class OccultCrescentHelper
{
    public static OccultCrescentState* GetState() => PublicContentOccultCrescent.GetState();

    public static bool IsStateAvailable()
    {
        PublicContentOccultCrescent* instance = PublicContentOccultCrescent.GetInstance();
        return instance != null && instance->StateLoaded && GetState() != null;
    }

    /// <summary>
    ///     Read the currencies as inventory items, not from <see cref="OccultCrescentState"/>.
    ///     <c>state->Silver</c> does not track the real balance — it read 9999 while the player
    ///     actually held 309, so it never produced a delta and the per-hour rate sat at zero, and
    ///     shopping believed it could always afford anything. <c>state->Gold</c> happened to agree,
    ///     but there is no reason to trust one and not the other when the item count is what the
    ///     game's own counter shows.
    /// </summary>
    /// <summary>
    ///     Pieces only — what the South Horn vendor actually accepts. Shopping must use this, not
    ///     the total: obols cannot pay for a piece-priced item.
    /// </summary>
    public static int GetSilverPieces() => GetCurrencyCount(ShopCatalog.SilverPieceItemId);

    public static int GetGoldPieces() => GetCurrencyCount(ShopCatalog.GoldPieceItemId);

    /// <summary>
    ///     Pieces plus obols. Each horn pays in only one of the two, so whichever you are earning
    ///     moves this total — which is what a per-hour rate needs. Tracking pieces alone reported
    ///     zero for anyone farming the horn that pays in obols.
    /// </summary>
    public static int GetSilverTotal() =>
        GetCurrencyCount(ShopCatalog.SilverPieceItemId) + GetCurrencyCount(ShopCatalog.SilverObolItemId);

    public static int GetGoldTotal() =>
        GetCurrencyCount(ShopCatalog.GoldPieceItemId) + GetCurrencyCount(ShopCatalog.GoldObolItemId);

    private static int GetCurrencyCount(uint itemId)
    {
        InventoryManager* inventory = InventoryManager.Instance();
        return inventory == null ? 0 : inventory->GetInventoryItemCount(itemId);
    }
}
