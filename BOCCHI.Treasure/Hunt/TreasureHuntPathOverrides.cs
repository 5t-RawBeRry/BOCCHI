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
    ///     Pads vnav cannot path to at all because the approach needs a jump, which BOCCHI has no
    ///     support for (movement is entirely vnav pathfinding). Excluded from the hunt so the run
    ///     does not burn the 30s stuck timeout on them every time.
    ///     These stay in treasure_route.json — deleting the entry here is all it takes to re-enable
    ///     one, which is the plan if vnavmesh gains jump support.
    /// </summary>
    private static readonly HashSet<(ZoneId Zone, uint NodeId)> UnreachableNodes =
    [
        // South Horn, wanderers-haven pad 2 — ledge at (-884.1, 3.8, -682.0), needs a short hop up.
        (ZoneId.SouthHorn, 1842u),
    ];

    /// <summary>True when this pad is knowingly unreachable and must be left out of the route.</summary>
    public static bool IsUnreachable(ZoneId zone, uint nodeId) => UnreachableNodes.Contains((zone, nodeId));

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
