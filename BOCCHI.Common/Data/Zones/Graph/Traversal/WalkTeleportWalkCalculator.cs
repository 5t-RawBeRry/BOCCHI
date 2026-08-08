using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Paths;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using System.Numerics;
using Path = Ocelot.Services.Pathfinding.Path;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public class WalkTeleportWalkCalculator : IGraphCandidateCalculator
{
    /// <summary>
    ///     Snap radius for "I'm already on a graph node." Camp return pad ↔ aetheryte is ~20–25y,
    ///     so 20f was too tight and forced a slow live vnav every depart-from-camp route.
    /// </summary>
    private const float GraphSnapRadius = 45f;

    /// <summary>Treat as basecamp without pathfinding (matches IsInBasecamp proximity).</summary>
    private const float CampSnapRadius = 80f;

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

        (Node Departure, float WalkCost)? resolved = await ResolveDeparture(graph, start, pathfinder);
        if (resolved == null)
        {
            return null;
        }

        Node departure = resolved.Value.Departure;
        float walkToDepartureCost = resolved.Value.WalkCost;

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
            BuildTeleportSteps(departure, inboundMeta.AetheryteId, goal, inbound, start));
    }

    private static async Task<(Node Departure, float WalkCost)?> ResolveDeparture(
        ZoneGraph graph,
        Vector3 start,
        IPathfinder pathfinder)
    {
        // Prefer camp aetheryte when standing in camp — never burn a vnav query just to leave base.
        Node? baseCamp = graph.GetBaseCampAetheryteNode();
        if (baseCamp != null && start.Distance2D(baseCamp.Position) <= CampSnapRadius)
        {
            return (baseCamp, start.Distance(baseCamp.Position));
        }

        if (graph.TryGetNode(start, GraphSnapRadius, out Node node))
        {
            if (node.IsTeleport())
            {
                return (node, start.Distance(node.Position));
            }

            List<Edge> connectedTeleports = graph.GetEdges(node.Id)
                .Where(e => graph.Nodes[e.To].IsTeleport())
                .OrderBy(e => e.Cost)
                .ToList();

            if (connectedTeleports.Count == 0)
            {
                return null;
            }

            Edge walkToNearestAethernet = connectedTeleports.First();
            return (graph.Nodes[walkToNearestAethernet.To], walkToNearestAethernet.Cost);
        }

        Node? nearest = graph.GetNearestTeleport(start);
        if (nearest == null)
        {
            return null;
        }

        Vector3 approach = nearest.GetCampStandOffPosition(start);
        Path walkToNearestTeleportPath = await pathfinder.Pathfind(new(approach)
        {
            From = start,
            AllowFlying = false
        });

        return (nearest, walkToNearestTeleportPath.Distance);
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
                PathStep.Pathfind(NavigationApproach.ResolveActivityApproach(goal, start))
            ]);

    /// <summary>
    ///     Pathfind (mountable) to departure aetheryte, then Teleport, then Pathfind to the goal.
    ///     Without the departure Pathfind, mid-map hops walk on foot via AetheryteApproach.
    /// </summary>
    private static List<PathStep> BuildTeleportSteps(
        Node departure,
        uint aetheryteId,
        Node goal,
        Node inbound,
        Vector3 start)
    {
        List<PathStep> steps = [];

        // Already Lifestream-ready (or in the idle band): Teleport's AetheryteApproach closes in.
        // Don't path around to the Dest-side stand-off (#158).
        float body = MathF.Max(2f, AethernetData.DefaultDeadRadius);
        float ready = body + AethernetNavigation.EdgeClearance + AethernetNavigation.PathfindArrivalRadius;
        if (start.Distance2D(departure.Position) > ready)
        {
            Vector3 standOff = departure.GetCampStandOffPosition(start);
            if (start.Distance2D(standOff) > AethernetNavigation.PathfindArrivalRadius + 0.5f)
            {
                steps.Add(PathStep.Pathfind(standOff, AethernetNavigation.PathfindArrivalRadius));
            }
        }

        steps.Add(PathStep.Teleport(aetheryteId));
        steps.Add(PathStep.Pathfind(NavigationApproach.ResolveActivityApproach(goal, inbound.Position)));
        return steps;
    }
}
