using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Plugin.Services;

namespace BOCCHI.Automator.Services.PotTreasure;

/// <summary>Uses Magical Elixir (item 2003296) via inventory / key items — AOCC pattern.</summary>
public sealed class MagicalElixirAssist(IPluginLog log)
{
    public unsafe bool HasElixir()
    {
        InventoryManager* inventory = InventoryManager.Instance();
        if (inventory == null)
        {
            return false;
        }

        if (inventory->GetInventoryItemCount(PotTreasureIds.MagicalElixirItemId, false) > 0)
        {
            return true;
        }

        InventoryContainer* keyItems = inventory->GetInventoryContainer(InventoryType.KeyItems);
        if (keyItems == null)
        {
            return false;
        }

        for (int i = 0; i < keyItems->Size; i++)
        {
            InventoryItem* slot = keyItems->GetInventorySlot(i);
            if (slot != null && !slot->IsEmpty() && slot->ItemId == PotTreasureIds.MagicalElixirItemId)
            {
                return true;
            }
        }

        return false;
    }

    public unsafe bool TryUse()
    {
        if (!EzThrottler.Throttle("PotTreasure::MagicalElixir", 1000))
        {
            return false;
        }

        if (!HasElixir())
        {
            log.Warning("Pot treasure: Magical Elixir not in inventory");
            return false;
        }

        AgentInventoryContext* agent = AgentInventoryContext.Instance();
        if (agent == null)
        {
            log.Warning("Pot treasure: AgentInventoryContext unavailable");
            return false;
        }

        // Prefer general UseItem path (AOCC production).
        long result = agent->UseItem(PotTreasureIds.MagicalElixirItemId);
        if (result is 0 or 1)
        {
            return true;
        }

        // Fallback: KeyItems slot.
        InventoryManager* inventory = InventoryManager.Instance();
        InventoryContainer* keyItems = inventory != null
            ? inventory->GetInventoryContainer(InventoryType.KeyItems)
            : null;
        if (keyItems == null)
        {
            log.Warning("Pot treasure: UseItem({Item}) returned {Result}", PotTreasureIds.MagicalElixirItemId, result);
            return false;
        }

        for (int i = 0; i < keyItems->Size; i++)
        {
            InventoryItem* slot = keyItems->GetInventorySlot(i);
            if (slot == null || slot->IsEmpty() || slot->ItemId != PotTreasureIds.MagicalElixirItemId)
            {
                continue;
            }

            long keyResult = agent->UseItem(slot->ItemId, InventoryType.KeyItems, (uint)slot->Slot);
            if (keyResult is 0 or 1)
            {
                return true;
            }

            log.Warning("Pot treasure: KeyItems UseItem returned {Result}", keyResult);
            return false;
        }

        log.Warning("Pot treasure: UseItem({Item}) returned {Result}", PotTreasureIds.MagicalElixirItemId, result);
        return false;
    }
}
