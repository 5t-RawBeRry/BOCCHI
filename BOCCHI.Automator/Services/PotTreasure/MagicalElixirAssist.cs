using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;

namespace BOCCHI.Automator.Services.PotTreasure;

/// <summary>Uses Magical Elixir (key item 2003296) for pot compass hints.</summary>
public sealed class MagicalElixirAssist(IPluginLog log)
{
    /// <summary>Game recast is ~5s — keep throttle slightly above so UseItem is not spammed on CD.</summary>
    private const int UseThrottleMs = 5500;

    public bool HasElixir() =>
        InventoryItemAssist.Has(PotTreasureIds.MagicalElixirItemId, includeKeyItems: true);

    public bool TryUse() =>
        InventoryItemAssist.TryUse(
            PotTreasureIds.MagicalElixirItemId,
            "PotTreasure::MagicalElixir",
            UseThrottleMs,
            log,
            "Pot treasure",
            tryKeyItems: true);
}
