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
    ///     South Horn piece count from inventory (not <see cref="OccultCrescentState"/>, which can
    ///     sit at 9999). Obols cannot pay a piece-priced vendor.
    /// </summary>
    public static int GetSilverPieces() => GetCurrencyCount(ShopCatalog.SilverPieceItemId);

    public static int GetGoldPieces() => GetCurrencyCount(ShopCatalog.GoldPieceItemId);

    /// <summary>Pieces plus obols — both horns, for per-hour rates.</summary>
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
