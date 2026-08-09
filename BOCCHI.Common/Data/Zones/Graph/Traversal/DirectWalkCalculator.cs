using BOCCHI.Common.Data.Paths;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using System.Numerics;
using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class DirectWalkCalculator : IGraphCandidateCalculator
{
    public string Key() => "DirectWalk";

    public async Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        float euclideanDistance2D = start.Distance2D(goal.Position);
        if (euclideanDistance2D > NavigationConstants.MaxDirectWalkDistance)
        {
            return null;
        }

        Vector3 approach = NavigationApproach.ResolveActivityApproach(goal, start);
        Path path = await pathfinder.Pathfind(new(approach)
        {
            From = start,
            AllowFlying = false
        });

        return new(path.Distance, [PathStep.Pathfind(approach, NavigationConstants.EventArrivalRadius)]);
    }
}
