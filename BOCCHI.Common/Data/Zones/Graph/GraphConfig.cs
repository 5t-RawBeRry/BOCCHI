using System.Numerics;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Graph;

public record ActivityData(int Id, Vector3 Position, float? CombatRadius = null);

public record CarrotData(int Id, Vector3 Position, int Level);

public record TreasureData(int Id, int Level);

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
        var result = await pathfinder.Pathfind(new PathfinderConfig(to)
        {
            From = from,
            AllowFlying = false,
        });

#if DEBUG
        DebugPathLines.Add(result.Nodes.ToList());
#endif

        return result.Distance;
    }

    public async Task<float> GetWalkingCost(Node from, Node to)
    {
        return await GetWalkingCost(from.Position, to.Position);
    }
}
