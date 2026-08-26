using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Shopping;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Ocelot.Extensions;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.PlayerState;
using System.Numerics;
using System.Runtime.InteropServices;

namespace BOCCHI.Services.Shopping;

/// <summary>
/// Approaches the Expedition Antiquarian when currency thresholds are hit and
/// purchases preferred catalog items from ShopExchangeCurrency.
/// </summary>
public sealed class ShoppingService
(
    ShoppingConfig config,
    IZoneProvider zones,
    IObjectTable objects,
    IPlayer player,
    IGameGui gui,
    IVNavmeshIpc vnav,
    ILogger<ShoppingService> logger
) : IOnUpdate
{
    private enum Phase
    {
        Idle,
        Approaching,
        OpeningMenu,
        Buying
    }

    private Phase phase = Phase.Idle;
    private DateTimeOffset buyCooldownUntil = DateTimeOffset.MinValue;

    public UpdateLimit UpdateLimit =>
        new()
        {
            Mode = UpdateLimitMode.Milliseconds,
            Limit = 250
        };

    public void Update()
    {
        if (!config.EnableAutoShop)
        {
            phase = Phase.Idle;
            return;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone() || zone.GetShoppingVendor() is not { } vendor)
        {
            phase = Phase.Idle;
            return;
        }

        int silver = OccultCrescentHelper.GetSilverPieces();
        int gold = OccultCrescentHelper.GetGoldPieces();
        bool shouldShop =
            config.PreferredItemIds.Count > 0
            && ((config.SilverThreshold > 0 && silver >= config.SilverThreshold)
                || (config.GoldThreshold > 0 && gold >= config.GoldThreshold));

        // Still process an already-open shop even when below threshold (finish buys).
        if (AddonHelpers.IsShopExchangeOpen() && config.PreferredItemIds.Count > 0)
        {
            TryHandleOpenShop(silver, gold);
            return;
        }

        if (!shouldShop && phase == Phase.Idle)
        {
            return;
        }

        if (DateTimeOffset.UtcNow < buyCooldownUntil)
        {
            return;
        }

        IGameObject? npc = objects
            .Where(o => o is { ObjectKind: ObjectKind.EventNpc, IsTargetable: true } && o.BaseId == vendor.DataId)
            .OrderBy(o => o.Position.Distance2D(player.Position))
            .FirstOrDefault();

        if (npc == null)
        {
            // Vendor only at basecamp — wait until player is near camp.
            if (player.Position.Distance2D(zone.GetAetherytePosition()) > 80f)
            {
                phase = Phase.Idle;
                return;
            }

            return;
        }

        float distance = npc.Position.Distance2D(player.Position);
        if (distance > 3.5f)
        {
            phase = Phase.Approaching;
            if (vnav.IsNavmeshReady() && EzThrottler.Throttle("Shopping::Path", 1000))
            {
                Vector3 dest = npc.Position.GetApproachPosition(player.Position, 2.5f);
                vnav.PathfindAndMoveCloseTo(dest, false, 1.5f);
            }

            return;
        }

        vnav.Stop();
        phase = Phase.OpeningMenu;
        unsafe
        {
            if (gui.GetAddonByName("SelectIconString", 1).Address != nint.Zero)
            {
                TrySelectShopMenu(0);
                return;
            }

            if (EzThrottler.Throttle("Shopping::Interact", 1000))
            {
                TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)npc.Address, false);
            }
        }
    }

    private unsafe bool TryHandleOpenShop(int silver, int gold)
    {
        if (!GenericHelpers.TryGetAddonByName("ShopExchangeCurrency", out AtkUnitBase* shop)
            || !GenericHelpers.IsAddonReady(shop))
        {
            return false;
        }

        phase = Phase.Buying;
        if (config.PreferredItemIds.Count == 0)
        {
            return true;
        }

        foreach (uint itemId in config.PreferredItemIds)
        {
            if (!ShopCatalog.TryGet(itemId, out ShopCatalogEntry entry))
            {
                continue;
            }

            int spendable = entry.CurrencyItemId == ShopCatalog.SilverPieceItemId
                ? silver - config.ReserveSilver
                : gold - config.ReserveGold;
            if (spendable < entry.Cost)
            {
                continue;
            }

            if (!EzThrottler.Throttle("Shopping::Buy", 750))
            {
                return true;
            }

            logger.Info($"[Shopping] buy item={entry.Name} ({entry.ItemId}) row={entry.RowIndex} cost={entry.Cost}");
            FirePurchaseCallback(shop, entry.RowIndex, 1);
            buyCooldownUntil = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(500);

            if (AddonHelpers.TryGetSelectYesno(out AddonSelectYesno* yesno))
            {
                try
                {
                    new AddonMaster.SelectYesno((nint)yesno).Yes();
                }
                catch
                {
                    // ignore — next tick retries
                }
            }

            return true;
        }

        if (EzThrottler.Throttle("Shopping::Close", 2000))
        {
            shop->FireCallbackInt(-1);
            phase = Phase.Idle;
            buyCooldownUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
            logger.Info("[Shopping] finished — no affordable preferred items");
        }

        return true;
    }

    private unsafe void TrySelectShopMenu(int menuIndex)
    {
        if (!EzThrottler.Throttle("Shopping::SelectMenu", 750))
        {
            return;
        }

        try
        {
            nint addon = gui.GetAddonByName("SelectIconString", 1).Address;
            if (addon == nint.Zero)
            {
                return;
            }

            new AddonMaster.SelectIconString(addon).Entries[menuIndex].Select();
        }
        catch (Exception ex)
        {
            logger.Warn($"[Shopping] SelectIconString failed: {ex.Message}");
        }
    }

    private static unsafe bool FirePurchaseCallback(AtkUnitBase* addon, uint rowIndex, int quantity)
    {
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
            values[0].SetInt(0);
            values[1].SetUInt(rowIndex);
            values[2].SetInt(quantity);
            return addon->FireCallback(4, values, true);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)values);
        }
    }
}
