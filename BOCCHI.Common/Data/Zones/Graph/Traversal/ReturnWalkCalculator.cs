using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class ReturnWalkCalculator : IGraphCandidateCalculator
{
    public string Key() => "ReturnWalk";

    public Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder) => Task.FromResult<TraversalCandidate?>(null);
}
