using FFXIVClientStructs.FFXIV.Client.Game;
using Ocelot.Actions;

namespace BOCCHI.Common.Data.Aethernet;

/// <summary>
///     Occult Return replaces hotbar Return inside the island. No cooldown.
///     <see cref="Ocelot.Actions.Action.GetRecastTime"/> on general action 8 is still the
///     overworld 15-minute timer and must not be used for availability.
/// </summary>
public static class OccultReturn
{
    public static unsafe bool CanCast()
    {
        ActionManager* actions = ActionManager.Instance();
        return actions != null
               && actions->GetActionStatus(Actions.Return.Type, Actions.Return.Id) == 0;
    }

    public static bool Cast() => Actions.Return.Cast();
}
