using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Aethernet;

public static class AethernetNavigation
{
    /// <summary>Soft vnav stop while closing on the camp approach ring.</summary>
    public const float PathfindArrivalRadius = 0.5f;

    /// <summary>
    ///     Stand-off from crystal center: path here, then hand off to Lifestream (do not path closer).
    /// </summary>
    public const float CampApproachRadius = 2.0f;

    public static Vector3 GetInteractPosition(this AethernetData data) => data.Destination != Vector3.Zero ? data.Destination : data.Position;

    public static Vector3 GetInteractPosition(this Node node)
    {
        if (node.Metadata is TeleportNodeMetadata { Destination: var destination } && destination != Vector3.Zero)
        {
            return destination;
        }

        return node.Position;
    }

    /// <summary>
    ///     Stand on the <see cref="CampApproachRadius"/> ring around the crystal (not the crystal center,
    ///     and not authored Destination pads that sit outside Lifestream range).
    /// </summary>
    public static Vector3 GetCampStandOffPosition(this AethernetData data, Vector3? from = null)
        => GetCampStandOffPosition(data.Position, data.GetInteractPosition(), from);

    public static Vector3 GetCampStandOffPosition(this Node node, Vector3? from = null)
        => GetCampStandOffPosition(node.Position, node.GetInteractPosition(), from);

    public static Vector3 GetCampStandOffPosition(Vector3 crystal, Vector3 interactOrHint, Vector3? from = null)
    {
        Vector3 dir = interactOrHint - crystal;
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.25f && from is { } player)
        {
            dir = player - crystal;
            dir.Y = 0f;
        }

        if (dir.LengthSquared() < 0.25f)
        {
            dir = new Vector3(1f, 0f, 0f);
        }

        Vector3 onRing = crystal + Vector3.Normalize(dir) * CampApproachRadius;
        return new Vector3(onRing.X, crystal.Y, onRing.Z);
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

    /// <summary>2y stand-off points around the nearest crystal for idle / Lifestream close-in.</summary>
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

        yield return nearest.GetCampStandOffPosition(from);

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

    public static Vector3 ResolveInteractDestination(Vector3 destination, IZone zone, Vector3? from = null)
    {
        foreach(AethernetData aetheryte in zone.EnumerateAetherytes())
        {
            float toCrystal = destination.Distance2D(aetheryte.Position);
            float toDest = destination.Distance2D(aetheryte.GetInteractPosition());
            if (toCrystal <= 5f || toDest <= 1.5f)
            {
                return aetheryte.GetCampStandOffPosition(from);
            }
        }

        return destination;
    }

    public static PathStep ResolveAetherytePathStep(IPathStep step, IZone zone, Vector3? from = null)
    {
        if (step is not PathStep pathStep || pathStep.PathStepData is not Pathfind(var destination, var range))
        {
            return (PathStep)step;
        }

        Vector3 resolved = ResolveInteractDestination(destination, zone, from);
        if (resolved == destination)
        {
            return pathStep;
        }

        return PathStep.Pathfind(resolved, PathfindArrivalRadius);
    }

    public static AethernetData? FindAetheryte(this IZone zone, uint placeNameId)
    {
        return zone.EnumerateAetherytes()
            .FirstOrDefault(aetheryte => aetheryte.Id == placeNameId);
    }
}
