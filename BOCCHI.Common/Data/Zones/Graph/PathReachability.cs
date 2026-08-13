using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph;

/// <summary>
///     vnav reports <c>Distance 0</c> for a path it could not build (fewer than two nodes), which is
///     indistinguishable from "already there" by cost alone. Traversal picks the cheapest candidate,
///     so an unreachable route scored 0 beat every real one — that is how island shards used to win
///     (e.g. Unhallowed Hamlet to Eye to Eye).
/// </summary>
public static class PathReachability
{
    public static bool IsReachable(this Path path) => path.Nodes.Count >= 2;

    /// <summary>Path cost, or <see cref="float.PositiveInfinity"/> when vnav could not reach it.</summary>
    public static float CostOrUnreachable(this Path path) =>
        path.IsReachable() ? path.Distance : float.PositiveInfinity;
}
