using BOCCHI.Common.Data.Zones;
using System.Numerics;

namespace BOCCHI.Treasure.Hunt;

/// <summary>
///     Manual via-points for hunt walks where plain vnav hits hazards (wind updrafts, bad mesh).
///     Map coords use Dalamud MapUtil (SizeFactor 100, Offset ±1024) for North Horn.
/// </summary>
public static class TreasureHuntPathOverrides
{
    /// <summary>
    ///     Pads vnav cannot path to at all. Empty now: the Wanderer's Haven ledge pad (SH 1842) is
    ///     reachable again because vnav does path toward it and simply pins the character against
    ///     the ledge, which <see cref="Common.Services.StuckJumpAssist"/> now clears with a hop.
    ///     Only list a pad here if vnav genuinely cannot route to it — a pad that is merely awkward
    ///     is better left in, since the stuck watch skips it for that run anyway.
    ///     Pads stay in treasure_route.json either way, so adding or removing an entry here is the
    ///     only change needed.
    /// </summary>
    private static readonly HashSet<(ZoneId Zone, uint NodeId)> UnreachableNodes = [];

    /// <summary>
    ///     Visit on the authored route only — peel-off takes a shortcut that falls (#185).
    /// </summary>
    private static readonly HashSet<(ZoneId Zone, uint NodeId)> NoPeelNodes =
    [
        // North Horn Unhallowed Hamlet basement — stairs down from 2053. Authored order is fine.
        (ZoneId.NorthHorn, 2072u),
    ];

    /// <summary>True when this pad is knowingly unreachable and must be left out of the route.</summary>
    public static bool IsUnreachable(ZoneId zone, uint nodeId) => UnreachableNodes.Contains((zone, nodeId));

    /// <summary>True when radar must not divert onto this pad; the authored walk still visits it.</summary>
    public static bool ShouldNotPeel(ZoneId zone, uint nodeId) => NoPeelNodes.Contains((zone, nodeId));

    /// <summary>Reach before opening the coffer.</summary>
    private static readonly Dictionary<(ZoneId Zone, uint NodeId), Vector3[]> ApproachByNode = new()
    {
        // Suspended Masonry_9 — map ~5.4, 34.1; plain path cuts through wind.
        // Approach via map 3.4, 34.2 then the chest.
        [(ZoneId.NorthHorn, 2061)] =
        [
            new(-904f, 157.8f, 636f),
        ],
        // Suspended Masonry lower pad — map ~8.6, 35.8; vnav cuts off the island edge (#173).
        // Keep the near island via only — (-700,160,800) is off-mesh (~100y west) and pathfind fails.
        [(ZoneId.NorthHorn, 2058)] =
        [
            new(-640f, 160.1f, 780f),
        ],
        // Unhallowed Hamlet basement (2072) — stairs from 2053. Plain vnav drops west and sticks
        // around (-389, -76, 126) instead of finishing the stair run (#195 / #185).
        [(ZoneId.NorthHorn, 2072)] =
        [
            new(-210f, 3.5f, 108f),
            new(-245f, -45f, 118f),
            new(-275f, -80f, 124f),
        ],
    };

    /// <summary>Leave through after the coffer so the next leg does not re-enter the hazard.</summary>
    private static readonly Dictionary<(ZoneId Zone, uint NodeId), Vector3[]> DepartureByNode = new()
    {
        // Map 3.1, 34.3 safe exit.
        [(ZoneId.NorthHorn, 2061)] =
        [
            new(-919f, 157.8f, 641f),
        ],
        // Same near-island via as approach — do not use the off-mesh (-700,160,800) point.
        [(ZoneId.NorthHorn, 2058)] =
        [
            new(-640f, 160.1f, 780f),
        ],
        // Climb back out toward 2053 / next Unhallowed pads after the basement coffer.
        [(ZoneId.NorthHorn, 2072)] =
        [
            new(-275f, -80f, 124f),
            new(-245f, -45f, 118f),
            new(-210f, 3.5f, 108f),
        ],
    };

    public static bool TryGetApproach(ZoneId zone, uint nodeId, out IReadOnlyList<Vector3> vias)
    {
        if (ApproachByNode.TryGetValue((zone, nodeId), out Vector3[]? points))
        {
            vias = points;
            return true;
        }

        vias = Array.Empty<Vector3>();
        return false;
    }

    public static bool TryGetDeparture(ZoneId zone, uint nodeId, out IReadOnlyList<Vector3> vias)
    {
        if (DepartureByNode.TryGetValue((zone, nodeId), out Vector3[]? points))
        {
            vias = points;
            return true;
        }

        vias = Array.Empty<Vector3>();
        return false;
    }
}
