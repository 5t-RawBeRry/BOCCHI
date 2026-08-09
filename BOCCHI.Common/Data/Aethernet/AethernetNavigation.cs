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

    /// <summary>Magenta ring — solid body / Lifestream zone.</summary>
    public static float GetBodyRadius(this AethernetData data) => MathF.Max(2f, data.DeadRadius);

    /// <summary>Cyan ring — outer edge of the idle band.</summary>
    public static float GetIdleOuterRadius(this AethernetData data) => data.GetBodyRadius() + EdgeClearance;

    /// <summary>Midpoint of the idle band (between magenta and cyan).</summary>
    public static float GetIdleWaitRadius(this AethernetData data) =>
        data.GetBodyRadius() + (EdgeClearance * 0.5f);

    public static Vector3 GetCampStandOffPosition(this AethernetData data, Vector3? from = null)
        => GetRingPosition(data.Position, data.GetInteractPosition(), data.GetBodyRadius(), from);

    public static Vector3 GetCampStandOffPosition(this Node node, Vector3? from = null)
        => GetRingPosition(node.Position, node.GetInteractPosition(), MathF.Max(2f, AethernetData.DefaultDeadRadius), from);

    /// <summary>Prefer authored <see cref="AethernetData.DeadRadius"/> when the node maps to a zone shard.</summary>
    public static Vector3 GetCampStandOffPosition(this Node node, IZone zone, Vector3? from = null)
    {
        float body = MathF.Max(2f, AethernetData.DefaultDeadRadius);
        if (node.Metadata is TeleportNodeMetadata { AetheryteId: var id }
            && zone.FindAetheryte(id) is { } data)
        {
            body = data.GetBodyRadius();
        }

        return GetRingPosition(node.Position, node.GetInteractPosition(), body, from);
    }

    public static Vector3 GetIdleWaitPosition(this AethernetData data, Vector3? from = null)
        => GetRingPosition(data.Position, data.GetInteractPosition(), data.GetIdleWaitRadius(), from);

    private static Vector3 GetRingPosition(Vector3 crystal, Vector3 interactOrHint, float radius, Vector3? from = null)
    {
        // Prefer the player's side of the crystal when we know where they are (#158).
        Vector3 dir;
        if (from is { } player)
        {
            dir = player - crystal;
            dir.Y = 0f;
        }
        else
        {
            dir = interactOrHint - crystal;
            dir.Y = 0f;
        }

        if (dir.LengthSquared() < 0.25f)
        {
            dir = interactOrHint - crystal;
            dir.Y = 0f;
        }

        if (dir.LengthSquared() < 0.25f)
        {
            dir = new Vector3(1f, 0f, 0f);
        }

        // Field shards often author Destination inside the body disk. Do not push past that pad
        // along the ray (that ran into / through the crystal). Cap at the hint distance.
        float hintDist = interactOrHint.Distance2D(crystal);
        float standOff = hintDist > 0.5f ? MathF.Min(radius, hintDist) : radius;

        Vector3 onRing = crystal + Vector3.Normalize(dir) * standOff;
        return new Vector3(onRing.X, crystal.Y, onRing.Z);
    }

    public static IEnumerable<AethernetData> EnumerateAetherytes(this IZone zone) => zone.GetAetherytes();

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

    /// <summary>Idle can stop once within the cyan outer ring (or closer).</summary>
    public static bool IsWithinIdleWait(this IZone zone, Vector3 position)
    {
        return zone.EnumerateAetherytes()
            .Any(aetheryte =>
            {
                float stopRadius = aetheryte.GetIdleOuterRadius() + PathfindArrivalRadius + 0.5f;
                return position.Distance2D(aetheryte.Position) <= stopRadius;
            });
    }

    /// <summary>Idle wait spots spread through the magenta→cyan band.</summary>
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
        if (step is not PathStep pathStep || pathStep.PathStepData is not Pathfind(var destination, _))
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
