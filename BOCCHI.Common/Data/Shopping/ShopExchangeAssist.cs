using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BOCCHI.Common.Data.Shopping;

/// <summary>Live ShopExchangeCurrency / AgentShop row lookup by item id.</summary>
public static unsafe class ShopExchangeAssist
{
    public static bool TryFindRowIndex(uint itemId, out uint rowIndex)
    {
        rowIndex = 0;
        AgentShop* agent = AgentShop.Instance();
        if (agent == null || !agent->IsAgentActive() || agent->ItemReceive == null || agent->ItemReceiveCount <= 0)
        {
            return false;
        }

        Span<AgentShop.ShopItem> items = agent->ItemReceiveSpan;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].ItemId != itemId)
            {
                continue;
            }

            rowIndex = (uint)i;
            return true;
        }

        return false;
    }

    public static bool TryGetSelectedItemId(out uint itemId)
    {
        itemId = 0;
        AgentShop* agent = AgentShop.Instance();
        if (agent == null || !agent->IsAgentActive() || agent->ItemReceive == null)
        {
            return false;
        }

        int index = agent->SelectedItemIndex;
        if (index < 0 || index >= agent->ItemReceiveCount)
        {
            return false;
        }

        itemId = agent->ItemReceiveSpan[index].ItemId;
        return itemId != 0;
    }

    public static bool IsShopReady(AtkUnitBase* shop) =>
        shop != null && shop->IsReady && shop->IsVisible;
}
