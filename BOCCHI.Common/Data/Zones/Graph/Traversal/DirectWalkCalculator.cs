using BOCCHI.Common.Data.Paths;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using System.Numerics;
using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class DirectWalkCalculator : IGraphCandidateCalculator
{
    public string Key() => "DirectWalk";

    /// <summary>
    ///     Straight-line distance is never longer than the walk vnav would build, so it is an
    ///     admissible bound. The approach point is offset from the goal centre by at most the CE
    ///     combat radius (or the FATE stand-off), so subtract that to stay a true lower bound.
    ///     This replaces a flat 80y cutoff that removed walking from consideration entirely — which
    ///     is why veering off a long route made Illegal Mode Return to camp and start over, even
    ///     when it was already most of the way there.
    /// </summary>
    public float? LowerBoundCost(ZoneGraph graph, Vector3 start, Node goal)
    {
        float approachOffset = goal.Metadata is ActivityNodeMetadata { CombatRadius: > 0 } meta
            ? meta.CombatRadius
            : NavigationConstants.EventApproachMaxRadius;

        return MathF.Max(0f, start.Distance2D(goal.Position) - approachOffset);
    }

    public async Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        Vector3 approach = NavigationApproach.ResolveActivityApproach(goal, start);
        Path path = await pathfinder.Pathfind(new(approach)
        {
            From = start,
            AllowFlying = false
        });

        // No candidate at all when vnav cannot build the walk — scoring it by Distance meant an
        // unreachable direct walk came back as cost 0 and won the cheapest-candidate pick.
        if (!path.IsReachable())
        {
            return null;
        }

        return new(path.Distance, [PathStep.Pathfind(approach, NavigationConstants.EventArrivalRadius)]);
    }
}
