using System.Numerics;
using Ocelot.Extensions;

namespace BOCCHI.Common.Data.Zones;

public static class NavigationConstants
{
    public const float MaxDirectWalkDistance = 80f;

    public const float EventApproachMinRadius = 5f;

    public const float EventApproachMaxRadius = 20f;

    public const float CampApproachJitter = 30f;

    public const float CriticalEncounterRadiusPadding = 7f;
}

public static class NavigationApproach
{
    public static Vector3 GetEventPosition(Vector3 destination, Vector3 from)
    {
        var range = NavigationConstants.EventApproachMinRadius
                    + Random.Shared.NextSingle() * (NavigationConstants.EventApproachMaxRadius - NavigationConstants.EventApproachMinRadius);

        return destination.GetApproachPosition(from, range, NavigationConstants.CampApproachJitter);
    }
}
