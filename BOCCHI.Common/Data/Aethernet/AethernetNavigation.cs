using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Aethernet;

public static class AethernetNavigation
{
    /// <summary>Graph routing when exact interact points are unreachable.</summary>
    public const float AetherytePathfindArrivalRadius = 3.5f;

    /// <summary>Soft vnav stop while closing in for Lifestream.</summary>
    public const float PathfindArrivalRadius = 0.5f;

    /// <summary>
    ///     Idle stand-off: outside the stone base, inside
    ///     <see cref="AethernetData.LifestreamInteractRadius"/>.
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

    public static IEnumerable<AethernetData> EnumerateAetherytes(this IZone zone) => zone.GetAetherytes();

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

    /// <summary>Camp pads that are still inside Lifestream range.</summary>
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

        // Skip Destination pads outside Lifestream range (SH base camp is ~4.7y out).
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
            // Rewrite crystal-aimed paths only — not nearby event approaches.
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
