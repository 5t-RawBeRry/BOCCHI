using System.Numerics;

namespace BOCCHI.Treasure.Services;

/// <summary>Normalize coffer positions that the game exposes with bogus altitudes.</summary>
public static class TreasurePathing
{
    /// <summary>
    ///     Rewrite only known-bogus reveal altitudes (Y ≈ -500) so vnav can land.
    ///     Do not pull valid authored pads to the player's Y — that put North Horn
    ///     candidates off-mesh (e.g. Y 49 → 90) and failed polygon lookup.
    /// </summary>
    public static Vector3 PathablePosition(Vector3 position, float playerY)
    {
        if (MathF.Abs(position.Y + 500f) < 0.5f)
        {
            return position with { Y = playerY };
        }

        return position;
    }
}
