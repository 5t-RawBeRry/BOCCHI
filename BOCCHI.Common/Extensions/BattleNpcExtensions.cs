using Dalamud.Game.ClientState.Objects.Types;

namespace BOCCHI.Common.Extensions;

/// <summary>
///     Same semantics as pre-rewrite <c>IGameObjectEx</c> on master:
///     use Dalamud's resolved <see cref="IGameObject.TargetObject"/> so idle
///     actors with sentinel target id 0xE0000000 are treated as untargeted.
/// </summary>
public static class BattleNpcExtensions
{
    public static bool HasTarget(this IGameObject obj) => obj.TargetObject != null;

    public static bool IsTargetingPlayer(this IGameObject obj, IGameObject? player) =>
        player != null && obj.TargetObject?.Address == player.Address;
}
