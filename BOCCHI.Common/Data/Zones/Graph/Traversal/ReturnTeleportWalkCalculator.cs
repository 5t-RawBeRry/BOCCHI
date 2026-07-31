using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Paths;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class ReturnTeleportWalkCalculator : IGraphCandidateCalculator
{
    public string Key() => "ReturnTeleportWalk";

    public Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        if (graph.TryGetNode(start, AethernetData.InteractRadius, out Node nearby) && nearby.IsTeleport())
        {
            return Task.FromResult<TraversalCandidate?>(null);
        }

        Node? baseCampAetheryte = graph.GetBaseCampAetheryteNode();
        if (baseCampAetheryte != null && start.Distance2D(baseCampAetheryte.GetInteractPosition()) <= AethernetData.InteractRadius)
        {
            return Task.FromResult<TraversalCandidate?>(null);
        }

        Node? returnNode = graph.GetBaseCampReturnPositionNode();
        if (returnNode == null)
        {
            return Task.FromResult<TraversalCandidate?>(null);
        }

        Node? baseCampNode = graph.GetBaseCampAetheryteNode();
        if (baseCampNode == null)
        {
            return Task.FromResult<TraversalCandidate?>(null);
        }

        Edge? toBaseCampNodeEdge = graph.GetEdge(returnNode, baseCampNode);
        if (toBaseCampNodeEdge == null)
        {
            return Task.FromResult<TraversalCandidate?>(null);
        }

        Node? inbound = graph.GetInboundTeleport(goal);
        if (inbound?.Metadata is not TeleportNodeMetadata meta)
        {
            return Task.FromResult<TraversalCandidate?>(null);
        }

        Edge? walkToGoalFromInbound = graph.GetEdge(inbound, goal);
        if (walkToGoalFromInbound == null)
        {
            return Task.FromResult<TraversalCandidate?>(null);
        }

        // Return already lands at base camp — no aethernet hop.
        if (inbound.Type == NodeType.BaseCampAetheryte)
        {
            return Task.FromResult<TraversalCandidate?>(new(
                GraphTraverser.ReturnCost + toBaseCampNodeEdge.Cost + walkToGoalFromInbound.Cost,
                [
                    PathStep.Return(),
                    PathStep.Pathfind(
                        NavigationApproach.GetEventPosition(goal.Position, returnNode.Position),
                        NavigationConstants.EventApproachMaxRadius)
                ]));
        }

        return Task.FromResult<TraversalCandidate?>(new(
            GraphTraverser.ReturnCost + toBaseCampNodeEdge.Cost + GraphTraverser.TeleportCost + walkToGoalFromInbound.Cost,
            [
                PathStep.Return(),
                PathStep.Teleport(meta.AetheryteId),
                PathStep.Pathfind(
                    NavigationApproach.GetEventPosition(goal.Position, inbound.Position),
                    NavigationConstants.EventApproachMaxRadius)
            ]));
    }
}
