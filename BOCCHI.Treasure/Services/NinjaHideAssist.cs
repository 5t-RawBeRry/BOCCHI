using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace BOCCHI.Treasure.Services;

/// <summary>Real Ninja Hide (2245 / status 614) + optional gearset swap — AOCC DangerousTreasure pattern.</summary>
public sealed class NinjaHideAssist(IPlayer player, ICondition conditions, IPluginLog log)
{
    public const uint HideActionId = 2245;

    public const uint HiddenStatusId = 614;

    public const uint NinjaClassJobId = 30;

    private static readonly Ocelot.Actions.Action Hide = new(ActionType.Action, HideActionId);

    public bool IsStealthed =>
        player.PlayerCharacter?.StatusList.Has(HiddenStatusId) == true;

    public bool IsNinja =>
        player.GetClassJob()?.RowId == NinjaClassJobId;

    public bool IsMounted =>
        conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting];

    /// <summary>
    ///     Prepare stealth for a dangerous walk. Returns false while still equipping / dismounting / casting.
    /// </summary>
    public bool EnsureReady(int ninjaGearsetNumber)
    {
        if (!IsNinja)
        {
            if (ninjaGearsetNumber <= 0)
            {
                return false;
            }

            TryEquipGearset(ninjaGearsetNumber);
            return false;
        }

        if (IsMounted)
        {
            TryDismount();
            return false;
        }

        if (IsStealthed)
        {
            return true;
        }

        TryCastHide();
        return false;
    }

    public void TryDismount()
    {
        if (!IsMounted || conditions[ConditionFlag.Mounting])
        {
            return;
        }

        if (EzThrottler.Throttle("NinjaHide::Dismount", 500) && Actions.Dismount.CanCast())
        {
            Actions.Dismount.Cast();
        }
    }

    public void TryCastHide()
    {
        if (!IsNinja || IsStealthed || IsMounted)
        {
            return;
        }

        if (!EzThrottler.Throttle("NinjaHide::Cast", 750) || !Hide.CanCast())
        {
            return;
        }

        Hide.Cast();
    }

    public unsafe bool TryEquipGearset(int gearsetNumber)
    {
        if (gearsetNumber <= 0 || IsNinja)
        {
            return IsNinja;
        }

        if (!EzThrottler.Throttle("NinjaHide::Gearset", 1500))
        {
            return false;
        }

        RaptureGearsetModule* module = RaptureGearsetModule.Instance();
        if (module == null)
        {
            log.Warning("Ninja Hide: RaptureGearsetModule unavailable");
            return false;
        }

        // Gearset numbers are 1-based in the UI; module slots are 0-based.
        int slot = gearsetNumber - 1;
        if (!module->IsValidGearset(slot))
        {
            log.Warning("Ninja Hide: gearset {Number} is invalid or empty", gearsetNumber);
            return false;
        }

        RaptureGearsetModule.GearsetEntry* entry = module->GetGearset(slot);
        if (entry == null || entry->ClassJob == 0)
        {
            log.Warning("Ninja Hide: gearset {Number} missing or empty", gearsetNumber);
            return false;
        }

        if (entry->ClassJob != NinjaClassJobId)
        {
            log.Warning(
                "Ninja Hide: gearset {Number} is ClassJob {Job}, expected Ninja ({Ninja})",
                gearsetNumber,
                entry->ClassJob,
                NinjaClassJobId);
            return false;
        }

        int result = module->EquipGearset(slot);
        if (result != 0)
        {
            log.Warning("Ninja Hide: EquipGearset({Slot}) returned {Result}", slot, result);
            return false;
        }

        return false;
    }
}
