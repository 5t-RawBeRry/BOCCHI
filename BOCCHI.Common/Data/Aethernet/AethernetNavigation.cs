using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Common.Data.Aethernet;

public static class AethernetNavigation
{
    /// <summary>Soft vnav stop while closing on aetheryte rings.</summary>
    public const float PathfindArrivalRadius = 0.5f;

    /// <summary>
    ///     Width of the idle band past the solid body (magenta→cyan). Same as
    ///     <see cref="AethernetData.LifestreamEdgeClearance"/>.
    /// </summary>
    public const float EdgeClearance = AethernetData.LifestreamEdgeClearance;

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
    ///     Magenta ring — solid body / Lifestream zone. Must be inside this to teleport.
    /// </summary>
    public static float GetBodyRadius(this AethernetData data) => MathF.Max(2f, data.DeadRadius);

    /// <summary>
    ///     Cyan ring — outer edge of the idle band (body + 2y). Idle waits between magenta and cyan.
    /// </summary>
    public static float GetIdleOuterRadius(this AethernetData data) => data.GetBodyRadius() + EdgeClearance;

    /// <summary>Midpoint of the idle band (between magenta and cyan).</summary>
    public static float GetIdleWaitRadius(this AethernetData data) =>
        data.GetBodyRadius() + (EdgeClearance * 0.5f);

    /// <summary>
    ///     Teleport approach: path to the magenta (Lifestream) ring — inside the solid-body radius.
    /// </summary>
    public static Vector3 GetCampStandOffPosition(this AethernetData data, Vector3? from = null)
        => GetRingPosition(data.Position, data.GetInteractPosition(), data.GetBodyRadius(), from);

    public static Vector3 GetCampStandOffPosition(this Node node, Vector3? from = null)
        => GetRingPosition(node.Position, node.GetInteractPosition(), 3.2f, from);

    public static Vector3 GetCampStandOffPosition(Vector3 crystal, Vector3 interactOrHint, Vector3? from = null)
        => GetRingPosition(crystal, interactOrHint, 3.2f, from);

    public static Vector3 GetIdleWaitPosition(this AethernetData data, Vector3? from = null)
        => GetRingPosition(data.Position, data.GetInteractPosition(), data.GetIdleWaitRadius(), from);

    private static Vector3 GetRingPosition(Vector3 crystal, Vector3 interactOrHint, float radius, Vector3? from = null)
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

        Vector3 onRing = crystal + Vector3.Normalize(dir) * radius;
        return new Vector3(onRing.X, crystal.Y, onRing.Z);
    }

    public static IEnumerable<AethernetData> EnumerateAetherytes(this IZone zone) => zone.GetAetherytes();

    public static bool IsWithinInteractRange(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Any(aetheryte => position.Distance2D(aetheryte.GetInteractPosition()) <= AethernetData.InteractRadius
                              || position.Distance2D(aetheryte.Position) <= AethernetData.InteractRadius);
    }

    /// <summary>True when inside the magenta Lifestream ring (ready to teleport).</summary>
    public static bool IsWithinLifestreamRange(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Any(aetheryte =>
            {
                float ready = aetheryte.GetBodyRadius() + PathfindArrivalRadius;
                return position.Distance2D(aetheryte.Position) <= ready;
            });
    }

    /// <summary>
    ///     Idle can stop once within the cyan outer ring (already in the idle band or closer —
    ///     including inside magenta; do not pull further in).
    /// </summary>
    public static bool IsWithinIdleWait(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Any(aetheryte =>
            {
                float stopRadius = aetheryte.GetIdleOuterRadius() + PathfindArrivalRadius + 0.5f;
                return position.Distance2D(aetheryte.Position) <= stopRadius;
            });
    }

    /// <summary>Magenta ring candidates — Lifestream / teleport close-in.</summary>
    public static IEnumerable<Vector3> GetApproachCandidates(this IZone zone, Vector3 from)
    {
        AethernetData? nearest = NearestAetheryte(zone, from);
        if (nearest == null)
        {
            yield break;
        }

        float radius = nearest.GetBodyRadius();
        yield return nearest.GetCampStandOffPosition(from);
        foreach (Vector3 point in RingPoints(nearest.Position, radius))
        {
            yield return point;
        }
    }

    /// <summary>
    ///     Idle wait candidates spread through the band between magenta (Lifestream) and cyan
    ///     (outer), so retries do not all stack on one ring.
    /// </summary>
    public static IEnumerable<Vector3> GetIdleWaitCandidates(this IZone zone, Vector3 from)
    {
        AethernetData? nearest = NearestAetheryte(zone, from);
        if (nearest == null)
        {
            yield break;
        }

        float inner = nearest.GetBodyRadius() + 0.25f;
        float outer = nearest.GetIdleOuterRadius();
        if (outer <= inner)
        {
            outer = inner + EdgeClearance;
        }

        yield return nearest.GetIdleWaitPosition(from);

        // Alternate depth in the band (25% / 50% / 75%) while sweeping angle.
        const int steps = 12;
        for (int i = 0; i < steps; i++)
        {
            float bandT = ((i % 3) + 1) / 4f;
            float radius = inner + ((outer - inner) * bandT);
            float angle = i * 2f * MathF.PI / steps;
            Vector3 crystal = nearest.Position;
            yield return crystal + new Vector3(
                MathF.Cos(angle) * radius,
                0f,
                MathF.Sin(angle) * radius);
        }
    }

    private static AethernetData? NearestAetheryte(IZone zone, Vector3 from) =>
        zone.EnumerateAetherytes()
            .OrderBy(aetheryte => from.Distance2D(aetheryte.Position))
            .FirstOrDefault();

    private static IEnumerable<Vector3> RingPoints(Vector3 crystal, float radius)
    {
        const int steps = 12;
        for (int i = 0; i < steps; i++)
        {
            float angle = i * 2f * MathF.PI / steps;
            yield return crystal + new Vector3(
                MathF.Cos(angle) * radius,
                0f,
                MathF.Sin(angle) * radius);
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
        foreach (AethernetData aetheryte in zone.EnumerateAetherytes())
        {
            float toCrystal = destination.Distance2D(aetheryte.Position);
            float toDest = destination.Distance2D(aetheryte.GetInteractPosition());
            if (toCrystal <= aetheryte.GetIdleOuterRadius() + 2f || toDest <= 1.5f)
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
