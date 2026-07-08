using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using Ocelot.Extensions;

namespace BOCCHI.Common.Data.Zones;

public static class NavigationApproach
{
    public static Vector3 GetEventPosition(Vector3 destination, Vector3 from)
    {
        var range = NavigationConstants.EventApproachMinRadius
                    + Random.Shared.NextSingle() * (NavigationConstants.EventApproachMaxRadius - NavigationConstants.EventApproachMinRadius);

        return destination.GetApproachPosition(from, range, NavigationConstants.CampApproachJitter);
    }
}
