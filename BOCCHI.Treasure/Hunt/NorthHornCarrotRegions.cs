using System.Numerics;
using BOCCHI.Common.Data.Zones;

namespace BOCCHI.Treasure.Hunt;

/// <summary>
///     North Horn carrot tour regions. Death-zone aggro is concentrated in the north
///     (Moldering / Sinking); keep those together after the safer center.
/// </summary>
internal enum NorthHornCarrotRegion
{
    Middle = 0,
    Northwest = 1,
    Northeast = 2,
}

internal static class NorthHornCarrotRegions
{
    public static readonly NorthHornCarrotRegion[] TourOrder =
    [
        NorthHornCarrotRegion.Middle,
        NorthHornCarrotRegion.Northwest,
        NorthHornCarrotRegion.Northeast,
    ];

    public static bool AppliesTo(ZoneId zone) => zone == ZoneId.NorthHorn;

    public static NorthHornCarrotRegion Classify(Vector3 position)
    {
        float x = position.X;
        float z = position.Z;

        // Suspended Masonry / far west ridge — babysit with NW.
        if (x < -400f)
        {
            return NorthHornCarrotRegion.Northwest;
        }

        // North of Unhallowed Hamlet: Moldering (west) vs Sinking Sanctuary (east).
        if (z < -300f)
        {
            return x < -200f
                ? NorthHornCarrotRegion.Northwest
                : NorthHornCarrotRegion.Northeast;
        }

        // East coast above the camp belt.
        if (x > 500f && z < -200f)
        {
            return NorthHornCarrotRegion.Northeast;
        }

        return NorthHornCarrotRegion.Middle;
    }
}
