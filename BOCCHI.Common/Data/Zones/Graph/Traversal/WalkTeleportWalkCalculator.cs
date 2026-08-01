using BOCCHI.Common.Data.Paths;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using System.Numerics;
using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class WalkTeleportWalkCalculator : IGraphCandidateCalculator
{
    public string Key() => "WalkTeleportWalk";

    public async Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder)
    {
        Node? inbound = graph.GetInboundTeleport(goal);
        if (inbound?.Metadata is not TeleportNodeMetadata inboundMeta)
        {
            return null;
        }

        Edge? walkToGoalFromInbound = graph.GetEdge(inbound, goal);
        if (walkToGoalFromInbound == null)
        {
            return null;
        }

        Node? departure;
        float walkToDepartureCost;

        if (graph.TryGetNode(start, 20f, out Node node))
        {
            if (node.IsTeleport())
            {
                departure = node;
                walkToDepartureCost = start.Distance(node.Position);
            }
            else
            {
                List<Edge> connectedTeleports = graph.GetEdges(node.Id)
                    .Where(e => graph.Nodes[e.To].IsTeleport())
                    .OrderBy(e => e.Cost)
                    .ToList();

                if (connectedTeleports.Count == 0)
                {
                    return null;
                }

                Edge walkToNearestAethernet = connectedTeleports.First();
                departure = graph.Nodes[walkToNearestAethernet.To];
                walkToDepartureCost = walkToNearestAethernet.Cost;
            }
        }
        else
        {
            departure = graph.GetNearestTeleport(start);
            if (departure == null)
            {
                return null;
            }

            Path walkToNearestTeleportPath = await pathfinder.Pathfind(new(departure.Position)
            {
                From = start,
                AllowFlying = false
            });

            walkToDepartureCost = walkToNearestTeleportPath.Distance;
        }

        // Same inbound shard as current = no-op teleport; just walk.
        if (IsSameAetheryte(departure, inbound, inboundMeta))
        {
            return BuildWalkOnly(start, goal, walkToDepartureCost + walkToGoalFromInbound.Cost);
        }

        // Field → base camp via shard loses to Return; leave to ReturnTeleportWalk.
        if (inbound.Type == NodeType.BaseCampAetheryte && departure.Type != NodeType.BaseCampAetheryte)
        {
            return null;
        }

        return new(
            walkToDepartureCost + GraphTraverser.TeleportCost + walkToGoalFromInbound.Cost,
            BuildTeleportSteps(inboundMeta.AetheryteId, goal, inbound));
    }

    private static bool IsSameAetheryte(Node departure, Node inbound, TeleportNodeMetadata inboundMeta)
    {
        if (departure.Id == inbound.Id)
        {
            return true;
        }

        return departure.Metadata is TeleportNodeMetadata departureMeta
               && departureMeta.AetheryteId == inboundMeta.AetheryteId;
    }

    private static TraversalCandidate BuildWalkOnly(Vector3 start, Node goal, float cost) =>
        new(
            cost,
            [
                // Destination is already offset via GetEventPosition — don't also give vnav a 20y arrival.
                PathStep.Pathfind(NavigationApproach.GetEventPosition(goal.Position, start))
            ]);

    private static List<PathStep> BuildTeleportSteps(uint aetheryteId, Node goal, Node inbound) =>
    [
        PathStep.Teleport(aetheryteId),
        PathStep.Pathfind(NavigationApproach.GetEventPosition(goal.Position, inbound.Position))
    ];
}
