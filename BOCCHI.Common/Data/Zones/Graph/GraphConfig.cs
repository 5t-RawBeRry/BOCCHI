using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;
using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph;

public record ActivityData(int Id, Vector3 Position, float? CombatRadius = null);
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

        // Unreachable paths report Distance 0 — treat as infinite so graph routing
        // does not prefer island shards (e.g. Unhallowed Hamlet → Eye to Eye).
        return result.Nodes.Count < 2 ? float.PositiveInfinity : result.Distance;
    }

    public async Task<float> GetWalkingCost(Node from, Node to) => await GetWalkingCost(from.Position, to.Position);
}
