using BOCCHI.Common.Data.Zones.Graph;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

public static class NavigationConstants
{
    public const float MaxDirectWalkDistance = 80f;

    /// <summary>Stop this close to FATE/CE center so we enter the engagement circle.</summary>
    public const float EventApproachMinRadius = 0f;

    public const float EventApproachMaxRadius = 5f;

    /// <summary>PathCalculator treats this as "arrived at event" — must be ≥ max approach.</summary>
    public const float EventArrivalRadius = 5f;

    public const float CampApproachJitter = 30f;

    /// <summary>
    /// Added to authored CE combat radius for <see cref="CriticalEncounter.Radius"/> / debug green ring.
    /// Red debug ring = padded − this (= combat radius).
    /// </summary>
    public const float CriticalEncounterRadiusPadding = 7f;

    /// <summary>Yellow debug ring inset from padded CE radius (green − this).</summary>
    public const float CriticalEncounterYellowInset = 2f;

    /// <summary>Fraction of combat radius treated as inside the CE registration (blue) circle.</summary>
    public const float CriticalEncounterRegistrationMaxRatio = 1.0f;

    /// <summary>Random stand-off ring while waiting for a predicted pot FATE (#112).</summary>
    public const float PotPrepositionMinRadius = 12f;

    public const float PotPrepositionMaxRadius = 32f;

    /// <summary>Euclidean distance above which long pathfinds should mount first.</summary>
    public const float MountMinDistance = 20f;

    /// <summary>Red debug / combat radius from padded <c>ce.Radius</c>.</summary>
    public static float CriticalEncounterRedRadius(float paddedRadius) =>
        MathF.Max(0f, paddedRadius - CriticalEncounterRadiusPadding);

    /// <summary>Yellow debug radius from padded <c>ce.Radius</c>.</summary>
    public static float CriticalEncounterYellowRadius(float paddedRadius) =>
        MathF.Max(0f, paddedRadius - CriticalEncounterYellowInset);

    /// <summary>Outer edge of the yellow–red wait band from authored combat radius.</summary>
    public static float CriticalEncounterYellowFromCombat(float combatRadius) =>
        combatRadius + CriticalEncounterRadiusPadding - CriticalEncounterYellowInset;
}

public static class NavigationApproach
{
    public static Vector3 GetEventPosition(Vector3 destination, Vector3 from)
    {
        float range = NavigationConstants.EventApproachMinRadius
                      + Random.Shared.NextSingle() * (NavigationConstants.EventApproachMaxRadius - NavigationConstants.EventApproachMinRadius);

        return destination.GetApproachPosition(from, range, NavigationConstants.CampApproachJitter);
    }

    /// <summary>
    ///     Random point in the yellow–red band (combat radius … combat+5) so pathing matches debug rings.
    /// </summary>
    public static Vector3 GetCriticalEncounterApproachPosition(Vector3 center, Vector3 from, float combatRadius)
    {
        float red = MathF.Max(0f, combatRadius);
        float yellow = NavigationConstants.CriticalEncounterYellowFromCombat(combatRadius);
        if (yellow < red)
        {
            yellow = red;
        }

        float approachRange = red + Random.Shared.NextSingle() * (yellow - red);
        return center.GetApproachPosition(from, approachRange);
    }

    public static Vector3 ResolveActivityApproach(Node goal, Vector3 from)
    {
        if (goal.Type == NodeType.CriticalEncounter
            && goal.Metadata is ActivityNodeMetadata { CombatRadius: > 0 } meta)
        {
            return GetCriticalEncounterApproachPosition(goal.Position, from, meta.CombatRadius);
        }

        return GetEventPosition(goal.Position, from);
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
