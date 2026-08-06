using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace BOCCHI.Treasure.Services;

/// <summary>Uses Fortune Carrot (item 48096) from inventory.</summary>
public sealed class FortuneCarrotAssist(IPluginLog log)
{
    public const uint ItemId = 48096;

    public unsafe int Count()
    {
        InventoryManager* inventory = InventoryManager.Instance();
        return inventory == null ? 0 : inventory->GetInventoryItemCount(ItemId, false);
    }

    public bool HasAny() => Count() > 0;

    public unsafe bool TryUse()
    {
        if (!EzThrottler.Throttle("CarrotHunt::FortuneCarrot", 1000))
        {
            return false;
        }

        if (!HasAny())
        {
            log.Warning("Carrot hunt: Fortune Carrot not in inventory");
            return false;
        }

        AgentInventoryContext* agent = AgentInventoryContext.Instance();
        if (agent == null)
        {
            log.Warning("Carrot hunt: AgentInventoryContext unavailable");
            return false;
        }

        long result = agent->UseItem(ItemId);
        if (result is 0 or 1)
        {
            return true;
        }

        log.Warning("Carrot hunt: UseItem({Item}) returned {Result}", ItemId, result);
        return false;
    }
}
