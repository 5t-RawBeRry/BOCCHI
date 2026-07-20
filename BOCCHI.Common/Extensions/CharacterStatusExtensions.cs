using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using Ocelot.Extensions;
namespace BOCCHI.Common.Extensions;

public static class CharacterStatusExtensions
{
    public static uint GetRemainingMinutes(this IPlayerCharacter player, uint statusId)
    {
        if (!player.StatusList.TryGet(statusId, out IStatus status))
        {
            return 0;
        }

        return (uint)TimeSpan.FromSeconds(status.RemainingTime).TotalMinutes;
    }
}
