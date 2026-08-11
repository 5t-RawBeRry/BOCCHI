using BOCCHI.Common.Data.Zones.Graph;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

public static class NavigationConstants
{
    public const float MaxDirectWalkDistance = 80f;

    /// <summary>Player is considered at base camp within this distance of the aetheryte.</summary>
    public const float CampRadius = 80f;

    /// <summary>Stop this close to FATE/CE center so we enter the engagement circle.</summary>
    public const float EventApproachMinRadius = 0f;

    public const float EventApproachMaxRadius = 5f;

    /// <summary>PathCalculator treats this as "arrived at event" — must be ≥ max approach.</summary>
    public const float EventArrivalRadius = 5f;

    public const float CampApproachJitter = 30f;

    /// <summary>
    ///     Added to authored CE combat radius for <see cref="CriticalEncounter.Radius"/> / debug green.
    ///     Red debug = padded − this.
    /// </summary>
    public const float CriticalEncounterRadiusPadding = 7f;

    /// <summary>Yellow debug ring inset from padded CE radius (green − this).</summary>
    public const float CriticalEncounterYellowInset = 2f;

    /// <summary>
    ///     Square CEs: inside red = in the registration zone (same as circles).
    ///     Authored half-extent should match the game blue box; do not shrink again here.
    /// </summary>
    public const float CriticalEncounterSquareWaitInnerRatio = 1f;

    /// <summary>Circle CEs: inside red = in the registration zone (stop pulling inward).</summary>
    public const float CriticalEncounterCircleWaitInnerRatio = 1f;

    /// <summary>Square CEs: cyan stand (path target) as a fraction of red half-extent.</summary>
    public const float CriticalEncounterSquareStandRatio = 0.7f;

    /// <summary>Circle CEs: cyan stand ring (path target while waiting), as a fraction of red.</summary>
    public const float CriticalEncounterCircleStandRatio = 0.7f;

    /// <summary>Debug green pad beyond red for square CEs (same idea as circle pad).</summary>
    public const float CriticalEncounterSquareRadiusPadding = 7f;

    /// <summary>Circle travel stand-off around the cyan ring.</summary>
    public const float CriticalEncounterApproachMinRatio = 0.6f;

    /// <summary>Circle travel stand-off outer (≤ stand ratio).</summary>
    public const float CriticalEncounterApproachMaxRatio = 0.75f;

    /// <summary>Square CEs: max Chebyshev stand-off from center as a fraction of half-extent.</summary>
    public const float CriticalEncounterSquareApproachMaxRatio = 0.25f;

    /// <summary>Random stand-off ring while waiting for a predicted pot FATE.</summary>
    public const float PotPrepositionMinRadius = 12f;

    public const float PotPrepositionMaxRadius = 32f;

    /// <summary>Euclidean distance above which long pathfinds should mount first.</summary>
    public const float MountMinDistance = 20f;

    /// <summary>Red debug / combat radius from padded <c>ce.Radius</c>.</summary>
    public static float CriticalEncounterRedRadius(
        float paddedRadius,
        ActivityAreaShape shape = ActivityAreaShape.Circle)
    {
        float pad = shape == ActivityAreaShape.Square
            ? CriticalEncounterSquareRadiusPadding
            : CriticalEncounterRadiusPadding;
        return MathF.Max(0f, paddedRadius - pad);
    }

    /// <summary>Yellow debug radius from padded <c>ce.Radius</c>.</summary>
    public static float CriticalEncounterYellowRadius(float paddedRadius) =>
        MathF.Max(0f, paddedRadius - CriticalEncounterYellowInset);

    /// <summary>Inside-zone size (red for circles; tighter hold for squares).</summary>
    public static float CriticalEncounterWaitHoldRadius(float combatRadius, ActivityAreaShape shape)
    {
        if (combatRadius <= 0f)
        {
            return EventArrivalRadius;
        }

        float ratio = shape == ActivityAreaShape.Square
            ? CriticalEncounterSquareWaitInnerRatio
            : CriticalEncounterCircleWaitInnerRatio;
        return combatRadius * ratio;
    }

    /// <summary>Cyan debug / preferred stand size (inside red).</summary>
    public static float CriticalEncounterStandRadius(float combatRadius, ActivityAreaShape shape)
    {
        if (combatRadius <= 0f)
        {
            return EventArrivalRadius;
        }

        float ratio = shape == ActivityAreaShape.Square
            ? CriticalEncounterSquareStandRatio
            : CriticalEncounterCircleStandRatio;
        return combatRadius * ratio;
    }

    /// <summary>Padded outer (green) size from authored combat radius.</summary>
    public static float CriticalEncounterPaddedRadius(float combatRadius, ActivityAreaShape shape)
    {
        float pad = shape == ActivityAreaShape.Square
            ? CriticalEncounterSquareRadiusPadding
            : CriticalEncounterRadiusPadding;
        return combatRadius + pad;
    }

    /// <summary>
    ///     True when <paramref name="point"/> is safely inside the CE registration area.
    ///     <paramref name="combatRadius"/> is authored combat size (circle radius or square half-extent).
    /// </summary>
    public static bool IsInsideCriticalEncounterWaitArea(
        Vector3 center,
        float combatRadius,
        ActivityAreaShape shape,
        Vector3 point)
    {
        float hold = CriticalEncounterWaitHoldRadius(combatRadius, shape);

        if (shape == ActivityAreaShape.Square)
        {
            // Chebyshev: keep pathing until clearly inside the blue square, not on the rim.
            float dx = MathF.Abs(point.X - center.X);
            float dz = MathF.Abs(point.Z - center.Z);
            return MathF.Max(dx, dz) <= hold;
        }

        return point.Distance2D(center) <= hold;
    }
}

public static class NavigationApproach
{
    public static Vector3 GetEventPosition(Vector3 destination, Vector3 from)
    {
        float range = NavigationConstants.EventApproachMinRadius
                      + Random.Shared.NextSingle() * (NavigationConstants.EventApproachMaxRadius - NavigationConstants.EventApproachMinRadius);

        return destination.GetApproachPosition(from, range, NavigationConstants.CampApproachJitter);
    }

    /// <summary>Random point inside the combat area so travel lands on the blue registration zone.</summary>
    public static Vector3 GetCriticalEncounterApproachPosition(
        Vector3 center,
        Vector3 from,
        float combatRadius,
        ActivityAreaShape shape = ActivityAreaShape.Circle)
    {
        float red = MathF.Max(1f, combatRadius);
        if (shape == ActivityAreaShape.Square)
        {
            // Squares (e.g. A Beast Unleashed): land well inside the blue box.
            float maxFromCenter = MathF.Min(
                red * NavigationConstants.CriticalEncounterSquareApproachMaxRatio,
                NavigationConstants.EventApproachMaxRadius);
            float approachRange = Random.Shared.NextSingle() * maxFromCenter;
            Vector3 delta = from - center;
            float chebyshev = MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Z));
            if (chebyshev < 0.001f || approachRange < 0.001f)
            {
                return center;
            }

            float scale = approachRange / chebyshev;
            return center + new Vector3(delta.X * scale, 0f, delta.Z * scale);
        }

        float min = red * NavigationConstants.CriticalEncounterApproachMinRatio;
        float max = red * NavigationConstants.CriticalEncounterApproachMaxRatio;
        if (max < min)
        {
            max = min;
        }

        float approachRangeCircle = min + Random.Shared.NextSingle() * (max - min);
        return center.GetApproachPosition(from, approachRangeCircle);
    }

    public static Vector3 ResolveActivityApproach(Node goal, Vector3 from)
    {
        if (goal.Type == NodeType.CriticalEncounter
            && goal.Metadata is ActivityNodeMetadata { CombatRadius: > 0 } meta)
        {
            return GetCriticalEncounterApproachPosition(goal.Position, from, meta.CombatRadius, meta.AreaShape);
        }

        return GetEventPosition(goal.Position, from);
    }

    /// <summary>
    ///     World / non-Illegal PathTo: use CE inner stand-off when the destination is a known CE.
    /// </summary>
    public static bool TryResolveCriticalEncounterApproach(
        IZone zone,
        Vector3 destination,
        Vector3 from,
        out Vector3 approach,
        out ActivityData? activity)
    {
        const float matchRadius = 80f;
        foreach (ActivityData candidate in zone.GetCriticalEncounterData())
        {
            if (candidate.CombatRadius is not { } radius || radius <= 0f)
            {
                continue;
            }

            if (destination.Distance2D(candidate.Position) > matchRadius)
            {
                continue;
            }

            activity = candidate;
            // Prefer PathTo destination as center (live CE position from World panel for squares).
            Vector3 center = destination;
            if (NavigationConstants.IsInsideCriticalEncounterWaitArea(
                    center, radius, candidate.AreaShape, from))
            {
                approach = from;
                return true;
            }

            approach = GetCriticalEncounterApproachPosition(
                center, from, radius, candidate.AreaShape);
            return true;
        }

        activity = null;
        approach = default;
        return false;
    }

    public static Vector3 GetPotPrepositionPosition(Vector3 potCenter, Vector3 from)
    {
        float dist = from.Distance2D(potCenter);
        if (dist >= NavigationConstants.PotPrepositionMinRadius
            && dist <= NavigationConstants.PotPrepositionMaxRadius)
        {
            return from;
        }

        float range = NavigationConstants.PotPrepositionMinRadius
                      + Random.Shared.NextSingle()
                      * (NavigationConstants.PotPrepositionMaxRadius - NavigationConstants.PotPrepositionMinRadius);
        float angle = Random.Shared.NextSingle() * MathF.PI * 2f;

        return potCenter + new Vector3(MathF.Cos(angle) * range, 0f, MathF.Sin(angle) * range);
    }
}
