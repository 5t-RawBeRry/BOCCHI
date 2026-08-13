using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;

namespace BOCCHI.Treasure.Services;

/// <summary>Uses Fortune Carrot (item 48096) from inventory.</summary>
public sealed class FortuneCarrotAssist(IPluginLog log)
{
    public const uint ItemId = 48096;

    public int Count() => InventoryItemAssist.Count(ItemId);

    public bool HasAny() => InventoryItemAssist.Has(ItemId);

    /// <param name="manual">Shorter throttle for the UI button; auto hunt uses the longer gate.</param>
    public bool TryUse(bool manual = false)
    {
        string throttleKey = manual ? "CarrotHunt::FortuneCarrotManual" : "CarrotHunt::FortuneCarrot";
        int throttleMs = manual ? 500 : 1000;
        return InventoryItemAssist.TryUse(ItemId, throttleKey, throttleMs, log, "Carrot hunt");
    }
}
