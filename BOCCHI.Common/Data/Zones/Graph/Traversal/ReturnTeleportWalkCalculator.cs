using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class ReturnTeleportWalkCalculator : IGraphCandidateCalculator
{
    public string Key()
    {
        return "ReturnTeleportWalk";
    }

    public async Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        if (graph.TryGetNode(start, AethernetData.InteractRadius, out var nearby) && nearby.IsTeleport())
        {
            return null;
        }

        var baseCampAetheryte = graph.GetBaseCampAetheryteNode();
        if (baseCampAetheryte != null && start.Distance2D(baseCampAetheryte.GetInteractPosition()) <= AethernetData.InteractRadius)
        {
            return null;
        }

        var returnNode = graph.GetBaseCampReturnPositionNode();
        if (returnNode == null)
        {
            return null;
        }

        var baseCampNode = graph.GetBaseCampAetheryteNode();
        if (baseCampNode == null)
        {
            return null;
        }

        var toBaseCampNodeEdge = graph.GetEdge(returnNode, baseCampNode);
        if (toBaseCampNodeEdge == null)
        {
            return null;
        }

        var inbound = graph.GetInboundTeleport(goal);
        if (inbound?.Metadata is not TeleportNodeMetadata meta)
        {
            return null;
        }

        var walkToGoalFromInbound = graph.GetEdge(inbound, goal);
        if (walkToGoalFromInbound == null)
        {
            return null;
        }

        return new TraversalCandidate(
            GraphTraverser.ReturnCost + toBaseCampNodeEdge.Cost + GraphTraverser.TeleportCost + walkToGoalFromInbound.Cost,
            [
                PathStep.Return(),
                PathStep.Pathfind(baseCampNode.GetInteractPosition(), AethernetNavigation.PathfindArrivalRadius),
                PathStep.Teleport(meta.AetheryteId),
                PathStep.Pathfind(NavigationApproach.GetEventPosition(goal.Position, inbound.Position)),

            ]
        );
    }
}
