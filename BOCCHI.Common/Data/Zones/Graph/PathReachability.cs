using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph;

/// <summary>
///     vnav reports <c>Distance 0</c> for a path it could not build (fewer than two nodes).
///     Treat that as unreachable so traversal does not prefer a failed path as free.
/// </summary>
public static class PathReachability
{
    public static bool IsReachable(this Path path) => path.Nodes.Count >= 2;

    /// <summary>Path cost, or <see cref="float.PositiveInfinity"/> when vnav could not reach it.</summary>
    public static float CostOrUnreachable(this Path path) =>
        path.IsReachable() ? path.Distance : float.PositiveInfinity;
}
