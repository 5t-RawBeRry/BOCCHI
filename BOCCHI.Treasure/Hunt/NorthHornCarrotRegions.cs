using System.Numerics;
using BOCCHI.Common.Data.Zones;

namespace BOCCHI.Treasure.Hunt;

/// <summary>
///     North Horn carrot tour regions. Death-zone aggro is concentrated in the north
///     (Moldering / Sinking); keep those together after the safer center and west ridge.
/// </summary>
internal enum NorthHornCarrotRegion
{
    Middle = 0,
    West = 1,
    Northwest = 2,
    Northeast = 3,
}

internal static class NorthHornCarrotRegions
{
    /// <summary>
    ///     Visit order. This is the single source of truth — <see cref="TourIndex"/> exists so
    ///     nothing compares raw enum values and quietly disagrees when this array is reordered.
    /// </summary>
    public static readonly NorthHornCarrotRegion[] TourOrder =
    [
        NorthHornCarrotRegion.Middle,
        NorthHornCarrotRegion.West,
        NorthHornCarrotRegion.Northwest,
        NorthHornCarrotRegion.Northeast,
    ];

    public static bool AppliesTo(ZoneId zone) => zone == ZoneId.NorthHorn;

    /// <summary>Position in <see cref="TourOrder"/>; unlisted regions sort last.</summary>
    public static int TourIndex(NorthHornCarrotRegion region)
    {
        int index = Array.IndexOf(TourOrder, region);
        return index < 0 ? int.MaxValue : index;
    }

    public static NorthHornCarrotRegion Classify(Vector3 position)
    {
        float x = position.X;
        float z = position.Z;

        // North of Unhallowed Hamlet — the death-zone band. Moldering (west) vs Sinking (east).
        // Checked first so the far-west ridge test below cannot swallow northern pads.
        if (z < -300f)
        {
            return x < -200f
                ? NorthHornCarrotRegion.Northwest
                : NorthHornCarrotRegion.Northeast;
        }

        // Suspended Masonry / far west ridge. Its own block rather than part of Northwest:
        // these sit in the southern half (z up to ~940), so folding them in gave "Northwest"
        // an ~1800 yalm north-south span and let peel-off wander the length of it.
        if (x < -400f)
        {
            return NorthHornCarrotRegion.West;
        }

        // East coast above the camp belt.
        if (x > 500f && z < -200f)
        {
            return NorthHornCarrotRegion.Northeast;
        }

        return NorthHornCarrotRegion.Middle;
    }
}
