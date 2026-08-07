using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;

namespace BOCCHI.Common.Data.Zones;

/// <summary>Shared dismount helper for state handlers.</summary>
public static class DismountAssist
{
    /// <summary>
    ///     If mounted (or mounting), try to dismount. Returns true when the caller should wait.
    /// </summary>
    public static bool TryDismount(ICondition conditions)
    {
        if (!conditions[ConditionFlag.Mounted] && !conditions[ConditionFlag.Mounting])
        {
            return false;
        }

        if (!conditions[ConditionFlag.Mounting])
        {
            Actions.Dismount.Cast();
        }

        return true;
    }
}
