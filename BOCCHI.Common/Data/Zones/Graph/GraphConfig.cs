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

    public readonly static List<List<Vector3>> Lines = [[]];


    public float TeleportCost { get; init; } = 10f;

    public async Task<float> GetWalkingCost(Vector3 from, Vector3 to)
    {
        logger.Info($"Calculating walking cost (from = {from:f2},  to = {to:f2})");
        var result = await pathfinder.Pathfind(new PathfinderConfig(to)
        {
            From = from,
            AllowFlying = false,
        });

        Lines.Add(result.Nodes.ToList());

        return result.Distance;
    }

    public async Task<float> GetWalkingCost(Node from, Node to)
    {
        return await GetWalkingCost(from.Position, to.Position);
    }
}
