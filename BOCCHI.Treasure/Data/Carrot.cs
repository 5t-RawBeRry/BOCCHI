using Dalamud.Game.ClientState.Objects.Types;
using System.Numerics;

namespace BOCCHI.Treasure.Data;

public sealed class Carrot(IGameObject obj)
{
    public static Vector4 Color { get; } = new(0.2f, 0.8f, 0.2f, 1f);

    public bool IsValid() => obj is { IsDead: false } && obj.IsValid();

    public Vector3 GetPosition() => obj.Position;
}
