using Ocelot.Ipc.VNavmesh;
using System.Numerics;

namespace BOCCHI.Treasure.Services;

/// <summary>Normalize coffer positions that the game exposes with bogus altitudes.</summary>
public static class TreasurePathing
{
    /// <summary>Horizontal slack when snapping an authored pad onto the navmesh.</summary>
    private const float SnapExtentXZ = 8f;

    /// <summary>Vertical search — authored / map Y is often tens of yalms off the floor.</summary>
    private const float SnapExtentY = 200f;

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

    /// <summary>
    ///     Project a coffer / pad onto the navmesh. Returns false when vnav has no polygon
    ///     (airborne authored Y, void) — callers must not PathfindAndMoveTo that point.
    ///     When the mesh is not ready, returns true with the unsnapped position.
    /// </summary>
    public static bool TrySnapToNavmesh(
        Vector3 position,
        float playerY,
        IVNavmeshIpc vnav,
        out Vector3 pathable)
    {
        pathable = PathablePosition(position, playerY);
        if (!vnav.IsAvailable() || !vnav.IsNavmeshReady())
        {
            return true;
        }

        if (TrySnap(vnav, pathable, out pathable))
        {
            return true;
        }

        Vector3 atPlayerAltitude = pathable with { Y = playerY };
        return TrySnap(vnav, atPlayerAltitude, out pathable);
    }

    private static bool TrySnap(IVNavmeshIpc vnav, Vector3 seed, out Vector3 snapped)
    {
        snapped = seed;
        if (!vnav.TryFindPointOnMesh(seed, SnapExtentXZ, SnapExtentY, out Vector3 onMesh))
        {
            return false;
        }

        snapped = vnav.TryFindPointOnFloor(onMesh, SnapExtentXZ, out Vector3 floored)
            ? floored
            : onMesh;
        return true;
    }
}
