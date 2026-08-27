using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.Shopping;
using BOCCHI.Common.Data.SupportJobs;
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
using Ocelot.Chain;
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
/// Soft-suspends other automation while shopping owns pathing.
/// </summary>
public sealed class ShoppingService
(
    ShoppingConfig config,
    IZoneProvider zones,
    IObjectTable objects,
    IPlayer player,
    IGameGui gui,
    IVNavmeshIpc vnav,
    IChainManager chainManager,
    IChainFactory chains,
    IAutomationModeGuard modeGuard,
    ISupportJobFactory supportJobs,
    IDataManager data,
    IUnlockState unlockState,
    ILogger<ShoppingService> logger
) : IOnUpdate
{
    private enum Phase
    {
        Idle,
        Traveling,
        Approaching,
        OpeningMenu,
        Buying
    }

    private Phase phase = Phase.Idle;
    private DateTimeOffset buyCooldownUntil = DateTimeOffset.MinValue;
    private bool priorityClaimed;
    private int desiredMenuIndex;
    private int? openedMenuIndex;
    private Task<ChainResult>? teleportChain;
    private readonly HashSet<uint> skippedMissingRows = [];

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
            if (priorityClaimed || phase != Phase.Idle)
            {
                AbortShopping(resumeAutomation: true);
            }

            return;
        }

        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone() || zone.GetShoppingVendor() is not { } vendor)
        {
            if (priorityClaimed || phase != Phase.Idle)
            {
                AbortShopping(resumeAutomation: true);
            }

            return;
        }

        ZoneId zoneId = zone.ZoneId;
        int silver = OccultCrescentHelper.GetActiveSilver(zoneId);
        int gold = OccultCrescentHelper.GetActiveGold(zoneId);
        bool thresholdHit =
            (config.SilverThreshold > 0 && silver >= config.SilverThreshold)
            || (config.GoldThreshold > 0 && gold >= config.GoldThreshold);
        bool shouldShop = thresholdHit && HasPendingGoals(zoneId);

        if (AddonHelpers.IsShopExchangeOpen() && HasPendingGoals(zoneId))
        {
            ClaimPriority();
            phase = Phase.Buying;
            TryHandleOpenShop(zoneId, silver, gold);
            return;
        }

        if (!shouldShop && phase == Phase.Idle)
        {
            return;
        }

        if (!shouldShop && phase != Phase.Idle && !AddonHelpers.IsShopExchangeOpen())
        {
            // Threshold cleared mid-trip with nothing left to finish — resume.
            FinishShopping();
            return;
        }

        if (DateTimeOffset.UtcNow < buyCooldownUntil)
        {
            return;
        }

        ShopCatalogEntry? next = PickNextPurchase(zoneId, preferLiveRow: false);
        if (next == null)
        {
            if (phase != Phase.Idle)
            {
                FinishShopping();
            }

            return;
        }

        desiredMenuIndex = next.Value.MenuIndex;
        ClaimPriority();

        if (teleportChain != null)
        {
            TickTeleport();
            return;
        }

        IGameObject? npc = FindVendor(vendor.DataId);
        if (npc == null)
        {
            TryTravelToCamp(zone, vendor.PreferredAethernetId);
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
                TrySelectShopMenu(desiredMenuIndex);
                return;
            }

            if (EzThrottler.Throttle("Shopping::Interact", 1000))
            {
                openedMenuIndex = null;
                TargetSystem.Instance()->InteractWithObject(
                    (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)npc.Address,
                    false);
            }
        }
    }

    private void ClaimPriority()
    {
        if (priorityClaimed)
        {
            return;
        }

        priorityClaimed = true;
        modeGuard.EnsureExclusive(AutomationMode.Shopping);
        logger.Info("[Shopping] soft-suspended other automation");
    }

    private void FinishShopping()
    {
        AbortShopping(resumeAutomation: true);
        buyCooldownUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        logger.Info("[Shopping] finished — nothing affordable left or trip complete");
    }

    private void AbortShopping(bool resumeAutomation)
    {
        phase = Phase.Idle;
        openedMenuIndex = null;
        skippedMissingRows.Clear();
        teleportChain = null;

        if (priorityClaimed && resumeAutomation)
        {
            priorityClaimed = false;
            modeGuard.NotifyShoppingEnded();
        }
        else if (!resumeAutomation)
        {
            priorityClaimed = false;
        }
    }

    private void TickTeleport()
    {
        phase = Phase.Traveling;
        if (teleportChain is not { IsCompleted: true })
        {
            return;
        }

        bool ok = teleportChain.IsCompletedSuccessfully && (teleportChain.Result?.IsSuccess ?? false);
        teleportChain = null;
        if (!ok)
        {
            logger.Warn("[Shopping] aethernet to camp failed — will path if vendor is in range");
        }
    }

    private void TryTravelToCamp(IZone zone, uint preferredAethernetId)
    {
        phase = Phase.Traveling;

        if (AetheryteApproach.IsAtPlaceName(zone, preferredAethernetId, player.Position)
            || zone.IsInBasecamp())
        {
            // At camp but vendor object not spawned yet — wait.
            return;
        }

        if (teleportChain != null)
        {
            return;
        }

        if (!EzThrottler.Throttle("Shopping::Teleport", 2000))
        {
            return;
        }

        vnav.Stop();
        teleportChain = chainManager.Manage(
            chains.Create($"Shopping::Teleport({preferredAethernetId})")
                .Then<AethernetTeleportChain, uint>(preferredAethernetId));
    }

    private IGameObject? FindVendor(uint dataId) =>
        objects
            .Where(o => o is { ObjectKind: ObjectKind.EventNpc, IsTargetable: true } && o.BaseId == dataId)
            .OrderBy(o => o.Position.Distance2D(player.Position))
            .FirstOrDefault();

    private unsafe bool TryHandleOpenShop(ZoneId zoneId, int silver, int gold)
    {
        if (!GenericHelpers.TryGetAddonByName("ShopExchangeCurrency", out AtkUnitBase* shop)
            || !GenericHelpers.IsAddonReady(shop))
        {
            return false;
        }

        openedMenuIndex ??= desiredMenuIndex;

        // Confirm Yesno from a previous buy tick first.
        if (AddonHelpers.TryGetSelectYesno(out AddonSelectYesno* yesno))
        {
            if (EzThrottler.Throttle("Shopping::Yesno", 500))
            {
                try
                {
                    new AddonMaster.SelectYesno((nint)yesno).Yes();
                }
                catch
                {
                    // next tick retries
                }
            }

            return true;
        }

        ShopCatalogEntry? next = PickNextPurchase(zoneId, preferLiveRow: true);
        if (next == null)
        {
            // Maybe need another menu for remaining preferred items.
            ShopCatalogEntry? otherMenu = PickNextPurchase(zoneId, preferLiveRow: false);
            if (otherMenu is { } switchTo && switchTo.MenuIndex != openedMenuIndex)
            {
                if (EzThrottler.Throttle("Shopping::CloseForMenu", 1000))
                {
                    shop->FireCallbackInt(-1);
                    desiredMenuIndex = switchTo.MenuIndex;
                    openedMenuIndex = null;
                    phase = Phase.OpeningMenu;
                    logger.Info($"[Shopping] switching to menu {desiredMenuIndex}");
                }

                return true;
            }

            if (EzThrottler.Throttle("Shopping::Close", 2000))
            {
                shop->FireCallbackInt(-1);
                FinishShopping();
            }

            return true;
        }

        ShopCatalogEntry entry = next.Value;
        if (!ShopExchangeAssist.TryFindRowIndex(entry.ItemId, out uint rowIndex))
        {
            skippedMissingRows.Add(entry.ItemId);
            logger.Debug($"[Shopping] item {entry.Name} ({entry.ItemId}) not in open shop — skip");
            return true;
        }

        if (!EzThrottler.Throttle("Shopping::Buy", 750))
        {
            return true;
        }

        logger.Info($"[Shopping] buy item={entry.Name} ({entry.ItemId}) row={rowIndex} cost={entry.Cost}");
        FirePurchaseCallback(shop, rowIndex, 1);
        NotePurchase(entry.ItemId);
        buyCooldownUntil = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(500);
        return true;
    }

    private void NotePurchase(uint itemId)
    {
        if (!config.Shopping.TryGetValue(itemId, out ShopListEntry? setting) || setting == null)
        {
            return;
        }

        if (setting.BuyAmount > 0)
        {
            setting.BuyAmount--;
        }
    }

    private bool HasPendingGoals(ZoneId zoneId)
    {
        foreach (uint itemId in config.ShoppingOrder)
        {
            if (!config.Shopping.TryGetValue(itemId, out ShopListEntry? setting) || setting == null)
            {
                continue;
            }

            if (!TryResolveEntry(itemId, zoneId, out ShopCatalogEntry entry))
            {
                continue;
            }

            if (ShopOwnership.TryIsOwned(entry, supportJobs, data, unlockState) == true)
            {
                continue;
            }

            if (setting.BuyAmount > 0)
            {
                return true;
            }

            if (setting.KeepAmount > InventoryItemAssist.Count(itemId))
            {
                return true;
            }

            if (setting.KeepBuying)
            {
                return true;
            }
        }

        return false;
    }

    private ShopCatalogEntry? PickNextPurchase(ZoneId zoneId, bool preferLiveRow)
    {
        // ICE priority: Buy amounts first, then Keep stock-ups, then Keep Buying sink.
        return PickByGoal(zoneId, preferLiveRow, ShopGoal.Buy)
               ?? PickByGoal(zoneId, preferLiveRow, ShopGoal.Keep)
               ?? PickByGoal(zoneId, preferLiveRow, ShopGoal.KeepBuying);
    }

    private enum ShopGoal
    {
        Buy,
        Keep,
        KeepBuying,
    }

    private ShopCatalogEntry? PickByGoal(ZoneId zoneId, bool preferLiveRow, ShopGoal goal)
    {
        List<ShopCatalogEntry> candidates = [];
        foreach (uint itemId in config.ShoppingOrder)
        {
            if (!config.Shopping.TryGetValue(itemId, out ShopListEntry? setting) || setting == null)
            {
                continue;
            }

            if (!TryResolveEntry(itemId, zoneId, out ShopCatalogEntry entry))
            {
                continue;
            }

            if (entry.ItemId == 0 || skippedMissingRows.Contains(entry.ItemId))
            {
                continue;
            }

            if (ShopOwnership.TryIsOwned(entry, supportJobs, data, unlockState) == true)
            {
                continue;
            }

            if (!MatchesGoal(setting, itemId, goal) || !CanAfford(entry))
            {
                continue;
            }

            candidates.Add(entry);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (preferLiveRow)
        {
            foreach (ShopCatalogEntry entry in candidates)
            {
                if (ShopExchangeAssist.TryFindRowIndex(entry.ItemId, out _))
                {
                    return entry;
                }
            }

            return null;
        }

        if (openedMenuIndex is { } open)
        {
            foreach (ShopCatalogEntry entry in candidates)
            {
                if (entry.MenuIndex == open)
                {
                    return entry;
                }
            }
        }

        return candidates[0];
    }

    private static bool MatchesGoal(ShopListEntry setting, uint itemId, ShopGoal goal) =>
        goal switch
        {
            ShopGoal.Buy => setting.BuyAmount > 0,
            ShopGoal.Keep => setting.KeepAmount > InventoryItemAssist.Count(itemId),
            ShopGoal.KeepBuying => setting.KeepBuying,
            _ => false,
        };

    private static bool TryResolveEntry(uint itemId, ZoneId zoneId, out ShopCatalogEntry entry)
    {
        foreach (ShopCatalogEntry e in ShopCatalog.EntriesForItem(itemId, zoneId))
        {
            entry = e;
            return true;
        }

        return ShopCatalog.TryGet(itemId, out entry);
    }

    private bool CanAfford(ShopCatalogEntry entry)
    {
        int have = OccultCrescentHelper.GetCurrencyCount(entry.CurrencyItemId);
        int reserve = 0;
        if (OccultCurrencies.IsSilverCurrency(entry.CurrencyItemId))
        {
            reserve = config.ReserveSilver;
        }
        else if (OccultCurrencies.IsGoldCurrency(entry.CurrencyItemId))
        {
            reserve = config.ReserveGold;
        }

        return have - reserve >= entry.Cost;
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

            AddonMaster.SelectIconString master = new(addon);
            if (menuIndex < 0 || menuIndex >= master.Entries.Length)
            {
                logger.Warn($"[Shopping] menu index {menuIndex} out of range ({master.Entries.Length})");
                return;
            }

            master.Entries[menuIndex].Select();
            openedMenuIndex = menuIndex;
            skippedMissingRows.Clear();
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
