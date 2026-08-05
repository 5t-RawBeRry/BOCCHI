using Ocelot.Extensions;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BOCCHI.Common.Data.Zones.Graph;

public class ZoneGraph
{
    private Dictionary<int, Guid> CriticalEncounterNodes = new();

    private Dictionary<Guid, EdgeSet> EdgeSets = new();

    private Dictionary<int, Guid> FateNodes = new();

    [JsonInclude] public Dictionary<Guid, Node> Nodes { get; private set; } = new();

    [JsonInclude] public Dictionary<Guid, List<Edge>> Edges { get; private set; } = new();

    public string ToJson()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters =
            {
                new NodeMetadataConverter(),
                new Vector3Converter()
            }
        };

        return JsonSerializer.Serialize(this, options);
    }

    public static ZoneGraph FromJson(string json)
    {
        JsonSerializerOptions options = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters =
            {
                new Vector3Converter(),
                new NodeMetadataConverter()
            }
        };

        return JsonSerializer.Deserialize<ZoneGraph>(json, options)!;
    }

    public void Cache()
    {
        foreach((Guid key, Node node) in Nodes)
        {
            if (node is { Type: NodeType.CriticalEncounter, Metadata: ActivityNodeMetadata cMeta })
            {
                CriticalEncounterNodes.Add(cMeta.Id, key);
            }

            if (node is { Type: NodeType.NormalFate or NodeType.PotFate, Metadata: ActivityNodeMetadata fMeta })
            {
                FateNodes.Add(fMeta.Id, key);
            }

            EdgeSets.Add(key, GetEdgeSetForNode(node));
        }
    }

    private EdgeSet GetEdgeSetForNode(Node node)
    {
        List<Edge> inbound = Edges.Values
            .SelectMany(edgeSet => edgeSet)
            .Where(edge => edge.To == node.Id).ToList();

        List<Edge> outbound = Edges.TryGetValue(node.Id, out List<Edge>? list) ? list : [];

        return new(inbound, outbound);
    }

    public void AddNode(Node node)
    {
        Nodes[node.Id] = node;
        if (!Edges.ContainsKey(node.Id))
        {
            Edges[node.Id] = [];
        }
    }

    public void AddEdge(Guid from, Guid to, float cost, EdgeType type)
    {
        if (!Nodes.ContainsKey(from) || !Nodes.ContainsKey(to))
        {
            throw new InvalidOperationException("Both nodes must exist before adding an edge.");
        }

        // Unreachable walks report PositiveInfinity — omit from the graph.
        if (!float.IsFinite(cost))
        {
            return;
        }

        Edges[from].Add(new()
        {
            Type = type,
            From = from,
            To = to,
            Cost = cost
        });
    }

    public void AddTwoWayEdge(Guid a, Guid b, float costAB, EdgeType type)
    {
        AddEdge(a, b, costAB, type);
        AddEdge(b, a, costAB, type);
    }

    public void AddTwoWayEdge(Guid a, Guid b, float costAB, float costBA, EdgeType type)
    {
        AddEdge(a, b, costAB, type);
        AddEdge(b, a, costBA, type);
    }

    public IEnumerable<Edge> GetEdges(Guid nodeId) => Edges.TryGetValue(nodeId, out List<Edge>? list) ? list : [];

    public EdgeSet GetEdgeSet(Guid nodeId) => EdgeSets.TryGetValue(nodeId, out EdgeSet set) ? set : new([], []);

    public Node GetCriticalEncounterNode(int id) => Nodes[CriticalEncounterNodes[id]];

    public Node GetFateNode(int id) => Nodes[FateNodes[id]];

    public IEnumerable<Node> GetNodes(Func<Node, bool> predicate) => Nodes.Values.Where(predicate);

    public Edge? GetEdge(Node from, Node to)
    {
        if (!Edges.TryGetValue(from.Id, out List<Edge>? list))
        {
            return null;
        }

        return list.FirstOrDefault(e => e.To == to.Id);
    }

    public bool TryGetNode(Vector3 position, float maxDistance, out Node node)
    {
        node = null!;

        if (Nodes.Count == 0)
        {
            return false;
        }

        float maxDistSq = maxDistance * maxDistance;
        float bestDistSq = float.MaxValue;
        Node? best = null;

        foreach(Node n in Nodes.Values)
        {
            float distSq = Vector3.DistanceSquared(n.Position, position);
            if (distSq <= maxDistSq && distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = n;
            }
        }

        if (best == null)
        {
            return false;
        }

        node = best;

        return true;
    }

    public bool TryGetNode(Vector3 position, out Node node) => TryGetNode(position, 20f, out node);

    public Node? GetInboundTeleport(Node goal)
    {
        uint? preferredId = goal.Metadata is ActivityNodeMetadata { PreferredAethernetId: { } id } ? id : null;

        List<(Node Teleport, float Cost)> inbound = Edges
            .Where(kvp => Nodes[kvp.Key].IsTeleport())
            .SelectMany(kvp => kvp.Value
                .Where(e => e.To == goal.Id)
                .Select(e => (Teleport: Nodes[kvp.Key], Cost: e.Cost)))
            .Where(entry => !float.IsPositiveInfinity(entry.Cost) && !float.IsNaN(entry.Cost))
            .ToList();

        if (inbound.Count == 0)
        {
            return null;
        }

        if (preferredId is { } preferred)
        {
            (Node Teleport, float Cost) preferredInbound = inbound
                .Where(entry => entry.Teleport.Metadata is TeleportNodeMetadata tm && tm.AetheryteId == preferred)
                .OrderBy(entry => entry.Cost)
                .FirstOrDefault();
            if (preferredInbound.Teleport != null)
            {
                return preferredInbound.Teleport;
            }
        }

        return inbound.OrderBy(entry => entry.Cost).First().Teleport;
    }

    public IEnumerable<Node> GetNodesByTypes(params NodeType[] types)
    {
        HashSet<NodeType> set = types.ToHashSet();
        return Nodes.Values.Where(n => set.Contains(n.Type));
    }

    public Node? GetBaseCampReturnPositionNode() => GetNodesByTypes(NodeType.BaseCampReturnPosition).FirstOrDefault();

    public Node? GetBaseCampAetheryteNode() => GetNodesByTypes(NodeType.BaseCampAetheryte).FirstOrDefault();

    public IEnumerable<Node> GetTeleportNodes() => GetNodesByTypes(NodeType.BaseCampAetheryte, NodeType.AethernetShard);

    public IEnumerable<Node> GetActivityNodes() => GetNodesByTypes(NodeType.NormalFate, NodeType.PotFate, NodeType.CriticalEncounter);

    public Node? GetNearestTeleport(Vector3 pos)
    {
        return GetTeleportNodes()
            .OrderBy(n => Vector3.Distance(n.Position, pos))
            .FirstOrDefault();
    }

    public async Task ConnectToBaseCamp(List<Node> nodes, GraphConfig config)
    {
        const float MaxEuclideanDistance2D = 512f;

        Node? returnNode = GetBaseCampReturnPositionNode();
        if (returnNode == null)
        {
            return;
        }

        foreach(Node node in nodes)
        {
            float euclideanDistance2D = returnNode.Position.Distance2D(node.Position);
            if (euclideanDistance2D > MaxEuclideanDistance2D)
            {
                continue;
            }

            float cost = await config.GetWalkingCost(returnNode, node);

            AddEdge(returnNode.Id, node.Id, cost, EdgeType.Walk);
        }
    }


    public async Task ConnectToNearestTeleports(List<Node> nodes, GraphConfig config)
    {
        List<Node> teleports = GetTeleportNodes().ToList();

        foreach (Node node in nodes)
        {
            // Prefer authored aethernet when present (AOCCH preferred shard), else nearest few.
            // If the preferred shard cannot walk to the activity, fall back to scoring nearby shards.
            List<Node> candidateTeleports = teleports;
            if (node.Metadata is ActivityNodeMetadata { PreferredAethernetId: { } preferredId })
            {
                List<Node> preferred = teleports
                    .Where(t => t.Metadata is TeleportNodeMetadata tm && tm.AetheryteId == preferredId)
                    .ToList();
                if (preferred.Count > 0)
                {
                    candidateTeleports = preferred;
                }
            }

            // Score a few Euclidean-nearest shards sequentially. Parallel WhenAll flooded
            // vnav (Queries: 1 + N queued) and stalled actual movement pathfinds.
            List<Node> nearestTeleports = candidateTeleports
                .OrderBy(t => t.Position.Distance2D(node.Position))
                .Take(candidateTeleports == teleports ? 3 : candidateTeleports.Count)
                .ToList();

            float bestInboundCost = float.PositiveInfinity;
            Node? bestInbound = null;
            float bestOutboundCost = float.PositiveInfinity;
            Node? bestOutbound = null;

            foreach (Node teleport in nearestTeleports)
            {
                if (teleport.Metadata is not TeleportNodeMetadata meta)
                {
                    throw new("Teleport node metadata is not set");
                }

                float toActivity = await config.GetWalkingCost(meta.Destination, node.Position);
                if (toActivity < bestInboundCost)
                {
                    bestInboundCost = toActivity;
                    bestInbound = teleport;
                }

                float fromActivity = await config.GetWalkingCost(node.Position, meta.Destination);
                if (fromActivity < bestOutboundCost)
                {
                    bestOutboundCost = fromActivity;
                    bestOutbound = teleport;
                }
            }

            // Preferred shard unreachable (island / water gap) — retry nearest walkable shards.
            if ((bestInbound == null || float.IsPositiveInfinity(bestInboundCost))
                && candidateTeleports != teleports)
            {
                nearestTeleports = teleports
                    .OrderBy(t => t.Position.Distance2D(node.Position))
                    .Take(3)
                    .ToList();

                foreach (Node teleport in nearestTeleports)
                {
                    if (teleport.Metadata is not TeleportNodeMetadata meta)
                    {
                        throw new("Teleport node metadata is not set");
                    }

                    float toActivity = await config.GetWalkingCost(meta.Destination, node.Position);
                    if (toActivity < bestInboundCost)
                    {
                        bestInboundCost = toActivity;
                        bestInbound = teleport;
                    }

                    float fromActivity = await config.GetWalkingCost(node.Position, meta.Destination);
                    if (fromActivity < bestOutboundCost)
                    {
                        bestOutboundCost = fromActivity;
                        bestOutbound = teleport;
                    }
                }
            }

            if (bestInbound != null && !float.IsPositiveInfinity(bestInboundCost))
            {
                AddEdge(bestInbound.Id, node.Id, bestInboundCost, EdgeType.Walk);
            }

            if (bestOutbound != null && !float.IsPositiveInfinity(bestOutboundCost))
            {
                AddEdge(node.Id, bestOutbound.Id, bestOutboundCost, EdgeType.Walk);
            }
        }
    }

    public async Task ConnectToNearestAlike(List<Node> nodes, GraphConfig config, int max = 2, float max_euclidean_distance_2d = 256f)
    {
        for(int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];

            IEnumerable<Node> nearestOther = nodes.Skip(i + 1).Where(n => n.Id != node.Id).OrderBy(c => c.Position.Distance2D(node.Position)).Take(max);
            foreach(Node other in nearestOther)
            {
                float euclidean_distance_2d = node.Position.Distance2D(other.Position);
                if (euclidean_distance_2d > max_euclidean_distance_2d)
                {
                    continue;
                }

                float ab = await config.GetWalkingCost(node, other);
                float ba = await config.GetWalkingCost(other, node);

                AddTwoWayEdge(node.Id, other.Id, ab, ba, EdgeType.Walk);
            }
        }
    }
    public readonly record struct EdgeSet(List<Edge> Inbound, List<Edge> Outbound);
}
