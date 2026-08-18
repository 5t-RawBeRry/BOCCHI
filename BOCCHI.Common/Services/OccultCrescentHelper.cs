using BOCCHI.Common.Data.Shopping;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

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

    /// <summary>
    ///     Whether an aethernet shard is usable, keyed by the PlaceName id we already store on
    ///     <c>AethernetData.Id</c>. Reads the aethernet menu's own entry list, so no bit-order
    ///     guessing against <c>OccultCrescentState.UnlockedTeleportBitmask</c>.
    ///     <para>
    ///     <b>Fails open.</b> When the agent has no data — typically because the aethernet menu has
    ///     not been opened this session — this returns true. Wrongly reporting "locked" would break
    ///     routing that works today; wrongly reporting "unlocked" only falls back to the current
    ///     behaviour of trying and walking instead.
    ///     </para>
    /// </summary>
    public static bool IsAethernetUnlocked(uint placeNameId)
    {
        AgentTelepotTown* agent = AgentModule.Instance() == null
            ? null
            : (AgentTelepotTown*)AgentModule.Instance()->GetAgentByInternalId(AgentId.TelepotTown);

        if (agent == null || agent->Data == null)
        {
            return true;
        }

        ref AgentTelepotTownData data = ref *agent->Data;
        int count = Math.Min((int)data.AetheryteCount, data.Entries.Length);
        for (var i = 0; i < count; i++)
        {
            AgentTelepotTownData.AetheryteEntry entry = data.Entries[i];
            if (entry.PlaceNameId == placeNameId)
            {
                return !entry.IsLocked && !entry.IsUnusable;
            }
        }

        // Not listed at all — the menu has entries but not this one, so treat it as unavailable.
        return count == 0;
    }

    private static int GetCurrencyCount(uint itemId)
    {
        InventoryManager* inventory = InventoryManager.Instance();
        return inventory == null ? 0 : inventory->GetInventoryItemCount(itemId);
    }
}
