using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BOCCHI.Common.Data.Shopping;

/// <summary>Live ShopExchangeCurrency / AgentShop row lookup and category-tab switching.</summary>
public static unsafe class ShopExchangeAssist
{
    /// <summary>
    /// Weapons / Armor / Accessories / Others — fixed order on OC currency shops.
    /// </summary>
    public const int CategoryTabCount = 4;

    public static bool TryFindRowIndex(uint itemId, out uint rowIndex) =>
        TryGetListedOffer(itemId, out rowIndex, out _, out _);

    /// <summary>
    ///     Live Currency Exchange row for <paramref name="itemId"/>, plus the cost currency/qty
    ///     from AgentShop's parallel cost slots (same item can list at silver, gold, or amulet).
    /// </summary>
    public static bool TryGetListedOffer(
        uint itemId,
        out uint rowIndex,
        out uint currencyItemId,
        out uint cost)
    {
        rowIndex = 0;
        currencyItemId = 0;
        cost = 0;
        AgentShop* agent = AgentShop.Instance();
        if (agent == null || !agent->IsAgentActive() || agent->ItemReceive == null || agent->ItemReceiveCount <= 0)
        {
            return false;
        }

        Span<AgentShop.ShopItem> items = agent->ItemReceiveSpan;
        int found = -1;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].ItemId != itemId)
            {
                continue;
            }

            found = i;
            break;
        }

        if (found < 0)
        {
            return false;
        }

        rowIndex = (uint)found;
        Span<AgentShop.ShopItem> costs = agent->ItemCostSpan;
        if (costs.Length == 0)
        {
            return true;
        }

        int stride = costs.Length >= items.Length * 3
            ? 3
            : costs.Length >= items.Length
                ? Math.Max(1, costs.Length / items.Length)
                : 1;
        int start = found * stride;
        int end = Math.Min(start + stride, costs.Length);
        for (int i = start; i < end; i++)
        {
            if (costs[i].ItemId == 0 || costs[i].ItemCount == 0)
            {
                continue;
            }

            currencyItemId = costs[i].ItemId;
            cost = costs[i].ItemCount;
            return true;
        }

        return true;
    }

    /// <summary>
    /// Switch the open Currency Exchange category tab by index (0–3).
    /// Language-safe — does not match tab labels.
    /// </summary>
    public static bool TrySelectCategoryTab(AtkUnitBase* addon, int tabIndex)
    {
        if (addon == null || tabIndex < 0 || tabIndex >= CategoryTabCount)
        {
            return false;
        }

        AtkValue* values = (AtkValue*)Marshal.AllocHGlobal(4 * sizeof(AtkValue));
        if (values == null)
        {
            return false;
        }

        try
        {
            values[0] = default;
            values[1] = default;
            values[2] = default;
            values[3] = default;
            values[0].SetInt(4);
            values[1].SetInt(-1);
            values[2].SetInt(1);
            values[3].SetInt(tabIndex);
            return addon->FireCallback(4, values, true);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)values);
        }
    }
}
