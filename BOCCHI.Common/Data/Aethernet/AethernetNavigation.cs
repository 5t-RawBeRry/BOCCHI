using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Services.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Ocelot.Extensions;

namespace BOCCHI.Common.Data.Aethernet;

public static class AethernetNavigation
{
    /// <summary>
    /// How close vnav should try to get to the interact point before handing off to Lifestream.
    /// </summary>
    public const float PathfindArrivalRadius = AethernetData.InteractRadius - 1f;

    public const float DestinationMatchRadius = 15f;

    public static Vector3 GetInteractPosition(this AethernetData data)
    {
        return data.Destination != Vector3.Zero ? data.Destination : data.Position;
    }

    public static Vector3 GetInteractPosition(this Node node)
    {
        if (node.Metadata is TeleportNodeMetadata { Destination: var destination } && destination != Vector3.Zero)
        {
            return destination;
        }

        return node.Position;
    }

    public static IEnumerable<AethernetData> EnumerateAetherytes(this IZone zone)
    {
        return zone.GetAetherytes().Concat(zone.GetAethernetShards());
    }

    public static bool IsWithinInteractRange(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Any(aetheryte => position.Distance2D(aetheryte.GetInteractPosition()) <= AethernetData.InteractRadius);
    }

    public static Vector3 GetNearestInteractPosition(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Select(aetheryte => aetheryte.GetInteractPosition())
            .OrderBy(interact => position.Distance2D(interact))
            .FirstOrDefault();
    }

    public static Vector3 ResolveInteractDestination(Vector3 destination, IZone zone, float matchRadius = DestinationMatchRadius)
    {
        foreach (var aetheryte in zone.EnumerateAetherytes())
        {
            var interact = aetheryte.GetInteractPosition();
            if (destination.Distance2D(interact) <= matchRadius
                || destination.Distance2D(aetheryte.Position) <= matchRadius)
            {
                return interact;
            }
        }

        return destination;
    }

    public static PathStep ResolveAetherytePathStep(IPathStep step, IZone zone)
    {
        if (step is not PathStep pathStep || pathStep.PathStepData is not Pathfind(var destination, var range))
        {
            return (PathStep)step;
        }

        var resolved = ResolveInteractDestination(destination, zone);
        if (resolved == destination)
        {
            return pathStep;
        }

        return PathStep.Pathfind(
            resolved,
            range > 0f ? range : PathfindArrivalRadius);
    }

    public static AethernetData? FindAetheryte(this IZone zone, uint placeNameId)
    {
        return zone.EnumerateAetherytes()
            .FirstOrDefault(aetheryte => aetheryte.Id == placeNameId);
    }
}
