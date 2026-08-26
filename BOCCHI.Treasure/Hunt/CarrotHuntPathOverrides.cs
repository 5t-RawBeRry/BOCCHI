using BOCCHI.Common.Data.Zones;
using System.Numerics;

namespace BOCCHI.Treasure.Hunt;

/// <summary>
///     Manual via-points for Carrot Hunt where plain vnav takes a long detour around a jump
///     or wind cut. Map coords use Dalamud MapUtil (SizeFactor 100, Offset ±1024) for North Horn.
/// </summary>
public static class CarrotHuntPathOverrides
{
    /// <summary>Reach before the authored carrot pad.</summary>
    private static readonly Dictionary<(ZoneId Zone, int CarrotId), Vector3[]> ApproachByCarrot = new()
    {
        // West Suspended Masonry tip — map ~2.4, 35.9. Direct vnav has no walkable link over
        // the small jump, so it routes the long way around the mountain. Approach across the
        // island via the same on-mesh point as treasure 2061 (map ~3.4, 34.2).
        [(ZoneId.NorthHorn, 25)] =
        [
            new(-904f, 157.8f, 636f),
        ],
    };

    public static bool TryGetApproach(ZoneId zone, int carrotId, out IReadOnlyList<Vector3> vias)
    {
        if (ApproachByCarrot.TryGetValue((zone, carrotId), out Vector3[]? points))
        {
            vias = points;
            return true;
        }

        vias = Array.Empty<Vector3>();
        return false;
    }
}
