using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ocelot.Extensions;

namespace BOCCHI.Common.Data.Zones.Graph;

public class ZoneGraph
{
    public readonly record struct EdgeSet(List<Edge> Inbound, List<Edge> Outbound);

    [JsonInclude] public Dictionary<Guid, Node> Nodes { get; private set; } = new();

    [JsonInclude] public Dictionary<Guid, List<Edge>> Edges { get; private set; } = new();

    private Dictionary<int, Guid> CriticalEncounterNodes = new();

    private Dictionary<int, Guid> FateNodes = new();

    private Dictionary<Guid, EdgeSet> EdgeSets = new();

    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new NodeMetadataConverter(),
                new Vector3Converter(),
            },
        };

        return JsonSerializer.Serialize(this, options);
    }

    public static ZoneGraph FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            Converters =
            {
                new Vector3Converter(),
                new NodeMetadataConverter(),
            },
        };

        return JsonSerializer.Deserialize<ZoneGraph>(json, options)!;
    }

    public void Cache()
    {
        foreach (var (key, node) in Nodes)
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
        var inbound = Edges.Values
            .SelectMany(edgeSet => edgeSet)
            .Where(edge => edge.To == node.Id).ToList();

        var outbound = Edges.TryGetValue(node.Id, out var list) ? list : [];

        return new EdgeSet(inbound, outbound);
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

        Edges[from].Add(new Edge
        {
            Type = type,
            From = from,
            To = to,
            Cost = cost,
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

    public IEnumerable<Edge> GetEdges(Guid nodeId)
    {
        return Edges.TryGetValue(nodeId, out var list) ? list : [];
    }

    public EdgeSet GetEdgeSet(Guid nodeId)
    {
        return EdgeSets.TryGetValue(nodeId, out var set) ? set : new EdgeSet([], []);
    }

    public Node GetCriticalEncounterNode(int id)
    {
        return Nodes[CriticalEncounterNodes[id]];
    }

    public Node GetFateNode(int id)
    {
        return Nodes[FateNodes[id]];
    }

    public IEnumerable<Node> GetNodes(Func<Node, bool> predicate)
    {
        return Nodes.Values.Where(predicate);
    }

    public Edge? GetEdge(Node from, Node to)
    {
        if (!Edges.TryGetValue(from.Id, out var list))
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

        var maxDistSq = maxDistance * maxDistance;
        var bestDistSq = float.MaxValue;
        Node? best = null;

        foreach (var n in Nodes.Values)
        {
            var distSq = Vector3.DistanceSquared(n.Position, position);
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

    public bool TryGetNode(Vector3 position, out Node node)
    {
        return TryGetNode(position, 20f,  out node);
    }

    public Node? GetInboundTeleport(Node goal)
    {
        return Edges
            .Where(kvp => Nodes[kvp.Key].IsTeleport())
            .Where(kvp => kvp.Value.Any(e => e.To == goal.Id))
            .Select(kvp => Nodes[kvp.Key])
            .FirstOrDefault();
    }

    public IEnumerable<Node> GetNodesByTypes(params NodeType[] types)
    {
        var set = types.ToHashSet();
        return Nodes.Values.Where(n => set.Contains(n.Type));
    }

    public Node? GetBaseCampReturnPositionNode()
    {
        return GetNodesByTypes(NodeType.BaseCampReturnPosition).FirstOrDefault();
    }

    public Node? GetBaseCampAetheryteNode()
    {
        return GetNodesByTypes(NodeType.BaseCampAetheryte).FirstOrDefault();
    }

    public IEnumerable<Node> GetTeleportNodes()
    {
        return GetNodesByTypes(NodeType.BaseCampAetheryte, NodeType.AethernetShard);
    }

    public IEnumerable<Node> GetActivityNodes()
    {
        return GetNodesByTypes(NodeType.NormalFate, NodeType.PotFate, NodeType.CriticalEncounter);
    }

    public Node? GetNearestTeleport(Vector3 pos)
    {
        return GetTeleportNodes()
            .OrderBy(n => Vector3.Distance(n.Position, pos))
            .FirstOrDefault();
    }

    public async Task ConnectToBaseCamp(List<Node> nodes, GraphConfig config)
    {
        const float MaxEuclideanDistance2D = 512f;

        var returnNode = GetBaseCampReturnPositionNode();
        if (returnNode == null)
        {
            return;
        }

        foreach (var node in nodes)
        {
            var euclideanDistance2D = returnNode.Position.Distance2D(node.Position);
            if (euclideanDistance2D > MaxEuclideanDistance2D)
            {
                continue;
            }

            var cost = await config.GetWalkingCost(returnNode, node);

            AddEdge(returnNode.Id, node.Id, cost, EdgeType.Walk);
        }
    }


    public async Task ConnectToNearestTeleports(List<Node> nodes, GraphConfig config)
    {
        var teleports = GetTeleportNodes().ToList();

        foreach (var node in nodes)
        {

            var nearestTeleports = teleports.OrderBy(t => t.Position.Distance2D(node.Position)).Take(2);

            var costTasks = nearestTeleports.Select(async teleport =>
            {
                if (teleport.Metadata is not TeleportNodeMetadata meta)
                {
                    throw new Exception("Teleport node metadata is not set");
                }

                var toActivity = await config.GetWalkingCost(meta.Destination, node.Position);

                var fromActivity = await config.GetWalkingCost(node.Position, meta.Destination);

                return new
                {
                    Teleport = teleport,
                    ToActivity = toActivity,
                    FromActivity = fromActivity,
                };
            });

            var results = await Task.WhenAll(costTasks);

            var bestInbound = results
                .OrderBy(r => r.ToActivity)
                .First();

            var bestOutbound = results
                .OrderBy(r => r.FromActivity)
                .First();

            AddEdge(bestInbound.Teleport.Id, node.Id, bestInbound.ToActivity, EdgeType.Walk);
            AddEdge(node.Id, bestOutbound.Teleport.Id, bestOutbound.FromActivity, EdgeType.Walk);
        }
    }

    public async Task ConnectToNearestAlike(List<Node> nodes, GraphConfig config, int max = 2, float max_euclidean_distance_2d = 256f)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            var nearestOther = nodes.Skip(i + 1).Where(n => n.Id != node.Id).OrderBy(c => c.Position.Distance2D(node.Position)).Take(max);
            foreach (var other in nearestOther)
            {
                var euclidean_distance_2d = node.Position.Distance2D(other.Position);
                if (euclidean_distance_2d > max_euclidean_distance_2d)
                {
                    continue;
                }

                var ab = await config.GetWalkingCost(node, other);
                var ba = await config.GetWalkingCost(other, node);

                AddTwoWayEdge(node.Id, other.Id, ab, ba, EdgeType.Walk);
            }
        }
    }
}
