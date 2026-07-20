using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
namespace BOCCHI.Common.Extensions;

public static unsafe class BattleNpcExtensions
{
    public static bool HasTarget(this IGameObject obj)
    {
        if (obj.Address == nint.Zero)
        {
            return false;
        }

        return ((BattleChara*)obj.Address)->GetTargetId() != default;
    }

    public static bool IsTargetingPlayer(this IGameObject obj, IGameObject? player)
    {
        if (player == null || player.Address == nint.Zero || obj.Address == nint.Zero)
        {
            return false;
        }

        GameObjectId playerId = ((BattleChara*)player.Address)->GetGameObjectId();
        return ((BattleChara*)obj.Address)->GetTargetId() == playerId;
    }
}
