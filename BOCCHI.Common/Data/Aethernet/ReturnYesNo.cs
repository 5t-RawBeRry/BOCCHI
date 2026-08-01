using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BOCCHI.Common.Data.Aethernet;

/// <summary>
///     Pre-rewrite TeleporterModule filter: only auto-accept the Return/Demi-Return
///     confirmation, not shop / other SelectYesno dialogs.
/// </summary>
public static class ReturnYesNo
{
    public static unsafe bool IsReturnConfirmation(AtkUnitBase* addon)
    {
        if (addon == null || !addon->IsVisible)
        {
            return false;
        }

        // Master TeleporterModule: AtkValues[7] is Int == -1 only for Return confirm.
        if (addon->AtkValues[7].Type != AtkValueType.Int || addon->AtkValues[7].Int != -1)
        {
            return false;
        }

        return true;
    }

    public static unsafe bool TryAccept(AtkUnitBase* addon)
    {
        if (!IsReturnConfirmation(addon))
        {
            return false;
        }

        addon->FireCallbackInt(0);
        return true;
    }
}
