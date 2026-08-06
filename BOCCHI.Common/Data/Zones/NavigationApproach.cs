using BOCCHI.Common.Data.Zones.Graph;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

/// <summary>
///     Per-CE wait/approach ratios as fractions of combat (red) radius.
///     Default stays at the global red-ring wait; tighten only for reported CEs where the
///     blue registration box sits inside authored red (e.g. A Beast Unleashed).
/// </summary>
public readonly record struct CriticalEncounterWaitProfile(
    float HoldMaxRatio,
    float ApproachMinRatio,
    float ApproachMaxRatio);

public static class CriticalEncounterWaitProfiles
{
    public static readonly CriticalEncounterWaitProfile Default = new(
        HoldMaxRatio: 1.0f,
        ApproachMinRatio: NavigationConstants.CriticalEncounterApproachMinRatio,
        ApproachMaxRatio: NavigationConstants.CriticalEncounterApproachMaxRatio);

    /// <summary>
    ///     Authored combat radius reaches past the live blue registration for this CE —
    ///     holding at full red left bots outside participation (old 4.0.2.5 / Discord).
    /// </summary>
    private static readonly Dictionary<int, CriticalEncounterWaitProfile> ByEncounterId = new()
    {
        // A Beast Unleashed (North Horn)
        [56] = new(HoldMaxRatio: 0.70f, ApproachMinRatio: 0.35f, ApproachMaxRatio: 0.55f),
    };

    public static CriticalEncounterWaitProfile For(int encounterId) =>
        ByEncounterId.TryGetValue(encounterId, out CriticalEncounterWaitProfile profile)
            ? profile
            : Default;

    public static float HoldRadius(float combatRadius, int encounterId) =>
        MathF.Max(1f, combatRadius) * For(encounterId).HoldMaxRatio;
}

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

    /// <summary>
    ///     Fraction of combat (red) radius used as the outer edge of the CE stand-off ring.
    ///     Must stay &lt; 1 so we register / participate; yellow is debug-only.
    /// </summary>
    public const float CriticalEncounterApproachMaxRatio = 0.85f;

    /// <summary>Inner fraction of combat radius for the CE stand-off ring.</summary>
    public const float CriticalEncounterApproachMinRatio = 0.35f;

    /// <summary>Euclidean distance above which long pathfinds should mount first.</summary>
    public const float MountMinDistance = 20f;

    /// <summary>Red debug / combat radius from padded <c>ce.Radius</c>.</summary>
    public static float CriticalEncounterRedRadius(float paddedRadius) =>
        MathF.Max(0f, paddedRadius - CriticalEncounterRadiusPadding);

    /// <summary>Yellow debug radius from padded <c>ce.Radius</c>.</summary>
    public static float CriticalEncounterYellowRadius(float paddedRadius) =>
        MathF.Max(0f, paddedRadius - CriticalEncounterYellowInset);

    /// <summary>Outer edge of the yellow–red debug band from authored combat radius.</summary>
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
    ///     Random point inside the combat (red) ring so the player registers for the CE.
    ///     Waiting in the yellow–red band (outside red) looks fine on debug overlays but
    ///     does not count as participating on live (#140).
    /// </summary>
    /// <param name="encounterId">Optional CE id for per-encounter wait profiles.</param>
    public static Vector3 GetCriticalEncounterApproachPosition(
        Vector3 center,
        Vector3 from,
        float combatRadius,
        int encounterId = 0)
    {
        CriticalEncounterWaitProfile profile = CriticalEncounterWaitProfiles.For(encounterId);
        float red = MathF.Max(1f, combatRadius);
        float min = red * profile.ApproachMinRatio;
        float max = red * profile.ApproachMaxRatio;
        if (max < min)
        {
            max = min;
        }

        float approachRange = min + Random.Shared.NextSingle() * (max - min);
        return center.GetApproachPosition(from, approachRange);
    }

    public static Vector3 ResolveActivityApproach(Node goal, Vector3 from)
    {
        if (goal.Type == NodeType.CriticalEncounter
            && goal.Metadata is ActivityNodeMetadata { CombatRadius: > 0 } meta)
        {
            return GetCriticalEncounterApproachPosition(goal.Position, from, meta.CombatRadius, meta.Id);
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
