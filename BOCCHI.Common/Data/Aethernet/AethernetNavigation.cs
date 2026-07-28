using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Aethernet;

public static class AethernetNavigation
{
    /// <summary>
    ///     Coarse threshold for graph routing when exact interact points are unreachable.
    /// </summary>
    public const float AetherytePathfindArrivalRadius = 3.5f;

    /// <summary>
    ///     Soft vnav stop distance while closing in for Lifestream.
    /// </summary>
    public const float PathfindArrivalRadius = 0.5f;

    /// <summary>
    ///     Idle / Lifestream stand-off from the crystal. Outside the stone base, inside
    ///     <see cref="AethernetData.LifestreamInteractRadius"/> so a camp spot can teleport.
    /// </summary>
    public const float CampApproachRadius = 3.0f;

    public static Vector3 GetInteractPosition(this AethernetData data) => data.Destination != Vector3.Zero ? data.Destination : data.Position;

    public static Vector3 GetInteractPosition(this Node node)
    {
        if (node.Metadata is TeleportNodeMetadata { Destination: var destination } && destination != Vector3.Zero)
        {
            return destination;
        }

        return node.Position;
    }

    public static IEnumerable<AethernetData> EnumerateAetherytes(this IZone zone) => zone.GetAetherytes().Concat(zone.GetAethernetShards());

    public static bool IsWithinInteractRange(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Any(aetheryte => position.Distance2D(aetheryte.GetInteractPosition()) <= AethernetData.InteractRadius
                              || position.Distance2D(aetheryte.Position) <= AethernetData.InteractRadius);
    }

    public static bool IsWithinLifestreamRange(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Any(aetheryte => position.Distance2D(aetheryte.Position) <= AethernetData.LifestreamInteractRadius);
    }

    /// <summary>
    ///     Camp parking spots that are also close enough for Lifestream.
    /// </summary>
    public static IEnumerable<Vector3> GetApproachCandidates(this IZone zone, Vector3 from)
    {
        AethernetData? nearest = zone.EnumerateAetherytes()
            .OrderBy(aetheryte => from.Distance2D(aetheryte.Position))
            .FirstOrDefault();

        if (nearest == null)
        {
            yield break;
        }

        Vector3 crystal = nearest.Position;
        Vector3 interact = nearest.GetInteractPosition();

        // Only use authored Destination when it is already inside Lifestream range.
        // Base camp Destination is ~4.7y out — parking there left Pathfinding stuck unable to teleport.
        float maxPadDistance = AethernetData.LifestreamInteractRadius - PathfindArrivalRadius;
        if (interact.Distance2D(crystal) > 0.5f && interact.Distance2D(crystal) <= maxPadDistance)
        {
            yield return new Vector3(interact.X, crystal.Y, interact.Z);
        }

        Vector3 toPlayer = from - crystal;
        toPlayer.Y = 0f;
        if (toPlayer.LengthSquared() > 0.25f)
        {
            yield return crystal + Vector3.Normalize(toPlayer) * CampApproachRadius;
        }

        const int steps = 12;
        for (int i = 0; i < steps; i++)
        {
            float angle = i * 2f * MathF.PI / steps;
            yield return crystal + new Vector3(
                MathF.Cos(angle) * CampApproachRadius,
                0f,
                MathF.Sin(angle) * CampApproachRadius);
        }
    }

    public static Vector3 GetNearestInteractPosition(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Select(aetheryte => aetheryte.GetInteractPosition())
            .OrderBy(interact => position.Distance2D(interact))
            .FirstOrDefault();
    }

    public static Vector3 ResolveInteractDestination(Vector3 destination, IZone zone)
    {
        foreach(AethernetData aetheryte in zone.EnumerateAetherytes())
        {
            // Only rewrite paths aimed at the crystal itself — not event approaches
            // that happen to sit near a camp pad.
            if (destination.Distance2D(aetheryte.Position) <= 3f)
            {
                return aetheryte.GetInteractPosition();
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

        Vector3 resolved = ResolveInteractDestination(destination, zone);
        if (resolved == destination)
        {
            return pathStep;
        }

        return PathStep.Pathfind(
            resolved,
            range > 0f ? range : AetherytePathfindArrivalRadius);
    }

    public static AethernetData? FindAetheryte(this IZone zone, uint placeNameId)
    {
        return zone.EnumerateAetherytes()
            .FirstOrDefault(aetheryte => aetheryte.Id == placeNameId);
    }
}
