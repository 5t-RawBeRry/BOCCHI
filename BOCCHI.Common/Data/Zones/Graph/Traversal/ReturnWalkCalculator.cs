using System.Numerics;
using BOCCHI.Common.Data.Paths;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class ReturnWalkCalculator : IGraphCandidateCalculator
{
    public string Key()
    {
        return "ReturnWalk";
    }

    public Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        return Task.FromResult<TraversalCandidate?>(null);
    }
}
