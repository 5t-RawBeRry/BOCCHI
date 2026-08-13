using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;
using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph;

/// <summary>How CE join / combat area is measured around <see cref="ActivityData.Position"/>.</summary>
public enum ActivityAreaShape
{
    /// <summary>Euclidean radius (<see cref="ActivityData.CombatRadius"/>).</summary>
    Circle = 0,

    /// <summary>Axis-aligned square; <see cref="ActivityData.CombatRadius"/> is half-extent (center → edge).</summary>
    Square = 1,
}

/// <param name="Position">Path / wait destination (CE staging or FATE start).</param>
/// <param name="CombatRadius">Engage radius (circle) or half-extent (square) for CEs; unused for FATEs.</param>
/// <param name="PreferredAethernetId">PlaceNameId of preferred inbound shard, if any.</param>
/// <param name="AreaShape">Circle (default) or axis-aligned square (e.g. A Beast Unleashed).</param>
public record ActivityData(
    int Id,
    Vector3 Position,
    float? CombatRadius = null,
    uint? PreferredAethernetId = null,
    ActivityAreaShape AreaShape = ActivityAreaShape.Circle);

public record CarrotData(int Id, Vector3 Position, int Level);

public record TreasureData(int Id, int Level, Vector3? Position = null)
{
    private const float PositionMatchDistanceSquared = 4f;

    public bool Matches(uint treasureRowId, Vector3 worldPosition) =>
        Id == treasureRowId
        || Position is { } position && Vector3.DistanceSquared(position, worldPosition) <= PositionMatchDistanceSquared;
}

public record PotChestData(Vector3 Position, int Level);

public class GraphConfig(IPathfinder pathfinder, ILogger logger)
{
#if DEBUG
    public static readonly List<List<Vector3>> DebugPathLines = [];
#endif

    public float TeleportCost { get; init; } = 10f;

    public async Task<float> GetWalkingCost(Vector3 from, Vector3 to)
    {
        logger.Debug($"Calculating walking cost (from = {from:f2}, to = {to:f2})");
        Path result = await pathfinder.Pathfind(new(to)
        {
            From = from,
            AllowFlying = false
        });

#if DEBUG
        DebugPathLines.Add(result.Nodes.ToList());
#endif

        return result.CostOrUnreachable();
    }

    public async Task<float> GetWalkingCost(Node from, Node to) => await GetWalkingCost(from.Position, to.Position);
}
