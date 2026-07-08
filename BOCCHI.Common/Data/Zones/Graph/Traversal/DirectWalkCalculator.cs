
using System.Numerics;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class DirectWalkCalculator : IGraphCandidateCalculator
{
    public string Key()
    {
        return "DirectWalk";
    }

    public async Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        var euclideanDistance2D = start.Distance2D(goal.Position);
        if (euclideanDistance2D > NavigationConstants.MaxDirectWalkDistance)
        {
            return null;
        }

        var approach = NavigationApproach.GetEventPosition(goal.Position, start);
        var path = await pathfinder.Pathfind(new PathfinderConfig(approach)
        {
            From = start,
            AllowFlying = false,
        });

        return new(path.Distance, [PathStep.Pathfind(approach)]);
    }
}
