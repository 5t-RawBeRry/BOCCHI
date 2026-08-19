using Ocelot.Extensions;
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

    /// <summary>Rewrite Y ≈ -500 reveal altitudes. Do not snap authored pads to the player's Y.</summary>
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

        if (TrySnap(vnav, pathable, out Vector3 snapped) && IsNearSeed(pathable, snapped))
        {
            pathable = snapped;
            return true;
        }

        Vector3 atPlayerAltitude = pathable with { Y = playerY };
        if (TrySnap(vnav, atPlayerAltitude, out snapped) && IsNearSeed(atPlayerAltitude, snapped))
        {
            pathable = snapped;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Nearest-mesh can land on a cliff or island tens of yalms away. That is not this coffer.
    /// </summary>
    private static bool IsNearSeed(Vector3 seed, Vector3 snapped) =>
        seed.Distance2D(snapped) <= SnapExtentXZ * 1.5f;

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
