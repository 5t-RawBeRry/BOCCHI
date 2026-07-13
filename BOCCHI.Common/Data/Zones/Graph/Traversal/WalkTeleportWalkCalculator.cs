using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class WalkTeleportWalkCalculator : IGraphCandidateCalculator
{
    public string Key()
    {
        return "WalkTeleportWalk";
    }

    public async Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
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

        if (graph.TryGetNode(start, 20f, out var node))
        {
            if (node.IsTeleport())
            {
                if (node.Id == inbound.Id)
                {
                    return null;
                }

                var walkToNearestAethernetCost = start.Distance(node.Position);

                return new TraversalCandidate(
                    walkToNearestAethernetCost + GraphTraverser.TeleportCost + walkToGoalFromInbound.Cost,
                    [
                        PathStep.Pathfind(node.GetInteractPosition(), AethernetNavigation.PathfindArrivalRadius),
                        PathStep.Teleport(meta.AetheryteId),
                        PathStep.Pathfind(NavigationApproach.GetEventPosition(goal.Position, inbound.Position)),
                    ]);
            }
            else
            {
                var edges = graph.GetEdges(node.Id).OrderBy((e) => e.Cost);
                var connectedTeleports = edges.Where(e => graph.Nodes[e.To].IsTeleport()).ToList();
                if (connectedTeleports.Count == 0)
                {
                    return null;
                }

                var walkToNearestAethernet = connectedTeleports.First();
                var nearestAethernet = graph.Nodes[walkToNearestAethernet.To];

                var walkToNearestAethernetCost = walkToNearestAethernet.Cost;

                return new TraversalCandidate(
                    walkToNearestAethernetCost + GraphTraverser.TeleportCost + walkToGoalFromInbound.Cost,
                    [
                        PathStep.Pathfind(nearestAethernet.GetInteractPosition(), AethernetNavigation.PathfindArrivalRadius),
                        PathStep.Teleport(meta.AetheryteId),
                        PathStep.Pathfind(NavigationApproach.GetEventPosition(goal.Position, inbound.Position)),
                    ]);
            }
        }

        var nearestTeleport = graph.GetNearestTeleport(start);
        if (nearestTeleport == null)
        {
            return null;
        }

        var walkToNearestTeleportPath = await pathfinder.Pathfind(new PathfinderConfig(nearestTeleport.Position)
        {
            From = start,
            AllowFlying = false,
        });

        return new TraversalCandidate(
            walkToNearestTeleportPath.Distance + GraphTraverser.TeleportCost + walkToGoalFromInbound.Cost,
            [
                PathStep.Pathfind(nearestTeleport.GetInteractPosition(), AethernetNavigation.PathfindArrivalRadius),
                PathStep.Teleport(meta.AetheryteId),
                PathStep.Pathfind(NavigationApproach.GetEventPosition(goal.Position, inbound.Position)),
            ]);
    }
}
