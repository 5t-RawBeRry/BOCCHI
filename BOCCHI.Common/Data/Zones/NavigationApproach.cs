using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones;

public static class NavigationConstants
{
    public const float MaxDirectWalkDistance = 80f;

    /// <summary>Stop this close to FATE/CE center so we enter the engagement circle.</summary>
    public const float EventApproachMinRadius = 0f;

    public const float EventApproachMaxRadius = 5f;

    /// <summary>PathCalculator treats this as "arrived at event" — must be ≤ max approach.</summary>
    public const float EventArrivalRadius = 3f;

    public const float CampApproachJitter = 30f;

    public const float CriticalEncounterRadiusPadding = 7f;

    /// <summary>Euclidean distance above which long pathfinds should mount first.</summary>
    public const float MountMinDistance = 20f;
}

public static class NavigationApproach
{
    public static Vector3 GetEventPosition(Vector3 destination, Vector3 from)
    {
        float range = NavigationConstants.EventApproachMinRadius
                      + Random.Shared.NextSingle() * (NavigationConstants.EventApproachMaxRadius - NavigationConstants.EventApproachMinRadius);

        return destination.GetApproachPosition(from, range, NavigationConstants.CampApproachJitter);
    }
}
