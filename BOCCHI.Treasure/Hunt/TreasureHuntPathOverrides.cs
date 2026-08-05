using BOCCHI.Common.Data.Zones;
using System.Numerics;

namespace BOCCHI.Treasure.Hunt;

/// <summary>
///     Manual via-points for hunt walks where plain vnav hits hazards (wind updrafts, bad mesh).
///     Map coords use Dalamud MapUtil (SizeFactor 100, Offset ±1024) for North Horn.
/// </summary>
public static class TreasureHuntPathOverrides
{
    /// <summary>Reach before opening the coffer.</summary>
    private static readonly Dictionary<(ZoneId Zone, uint NodeId), Vector3[]> ApproachByNode = new()
    {
        // Suspended Masonry_9 — map ~5.4, 34.1; vnav cuts through wind and thrash-jumps (#Discord Alexandre).
        // Approach via map 3.4, 34.2 then the chest.
        [(ZoneId.NorthHorn, 2061)] =
        [
            new(-904f, 157.8f, 636f),
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
