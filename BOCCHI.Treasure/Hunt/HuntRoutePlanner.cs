using System.Numerics;
using System.Text.Json;
using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin.Services;

namespace BOCCHI.Treasure.Hunt;

public interface IHuntRoutePlanner
{
    HuntPathfinderState State { get; }

    Task<List<HuntPathfinderStep>> FindPath(Vector3 start, List<uint> nodes);
}

public abstract class HuntRoutePlanner(
    ZoneId zoneId,
    IPluginLog log,
    float returnCost = 300f,
    float teleportCost = 50f
) : IHuntRoutePlanner
{
    public HuntPathfinderState State { get; private set; } = HuntPathfinderState.None;

    private HuntNodeDataSchema data = new();

    protected abstract uint GetStartingNode(Vector3 start, List<uint> nodes);

    public Task<List<HuntPathfinderStep>> FindPath(Vector3 start, List<uint> nodes)
    {
        if (State != HuntPathfinderState.FileLoaded)
        {
            throw new InvalidOperationException("Hunt route data not loaded");
        }

        State = HuntPathfinderState.Pathfinding;

        var startNode = GetStartingNode(start, nodes);
        var graph = BuildCostGraph(nodes);
        var ordered = SolveTspNearestInsertion(startNode, nodes, graph);
        var steps = BuildStepPath(ordered, graph);

        steps.Insert(0, HuntPathfinderStep.WalkToDestination(startNode));

        PrintPath(steps);
        State = HuntPathfinderState.PathfindingDone;
        return Task.FromResult(steps);
    }

    protected void LoadFile(string filename)
    {
        State = HuntPathfinderState.LoadingFile;

        var file = HuntDataPaths.GetDataFile(zoneId, filename);
        if (!File.Exists(file))
        {
            log.Error($"Required hunt data file not found: {file}");
            return;
        }

        var json = File.ReadAllText(file);
        data = JsonSerializer.Deserialize<HuntNodeDataSchema>(json) ?? new HuntNodeDataSchema();
        State = HuntPathfinderState.FileLoaded;
    }

    protected (float Cost, List<HuntPathfinderStep> Steps) GetBestSteps(uint fromId, uint toId)
    {
        var bestCost = float.MaxValue;
        List<HuntPathfinderStep> bestSteps = [];

        if (data.NodeToNodeDistances.TryGetValue(fromId, out var directList))
        {
            var direct = directList.FirstOrDefault(x => x.Id == toId);
            if (direct.Id == toId)
            {
                bestCost = direct.Distance;
                bestSteps = [HuntPathfinderStep.WalkToDestination(toId)];
            }
        }

        if (data.NodeToAethernetDistances.TryGetValue(fromId, out var shardList) && shardList.Count > 0)
        {
            var fromShard = shardList.OrderBy(x => x.Distance).First();
            foreach (var (aethernet, list) in data.AethernetToNodeDistances)
            {
                var to = list.FirstOrDefault(x => x.Id == toId);
                if (to.Id != toId)
                {
                    continue;
                }

                var cost = fromShard.Distance + teleportCost + to.Distance;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSteps =
                    [
                        HuntPathfinderStep.WalkToAethernet(fromShard.Aethernet),
                        HuntPathfinderStep.TeleportToAethernet(aethernet),
                        HuntPathfinderStep.WalkToDestination(toId),
                    ];
                }
            }
        }

        foreach (var (aethernet, list) in data.AethernetToNodeDistances)
        {
            var to = list.FirstOrDefault(x => x.Id == toId);
            if (to.Id != toId)
            {
                continue;
            }

            var cost = returnCost + teleportCost + to.Distance;
            if (cost >= bestCost)
            {
                continue;
            }

            bestCost = cost;
            bestSteps = aethernet == HuntAethernet.BaseCamp
                ?
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.WalkToDestination(toId),
                ]
                :
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.TeleportToAethernet(aethernet),
                    HuntPathfinderStep.WalkToDestination(toId),
                ];
        }

        return (bestCost, bestSteps);
    }

    private Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> BuildCostGraph(List<uint> nodes)
    {
        var graph = new Dictionary<uint, Dictionary<uint, (float, List<HuntPathfinderStep>)>>();

        foreach (var from in nodes)
        {
            graph[from] = new Dictionary<uint, (float, List<HuntPathfinderStep>)>();
            foreach (var to in nodes)
            {
                if (from == to)
                {
                    continue;
                }

                graph[from][to] = GetBestSteps(from, to);
            }
        }

        return graph;
    }

    private static List<uint> SolveTspNearestInsertion(
        uint start,
        List<uint> nodes,
        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph
    )
    {
        if (nodes.Count == 1)
        {
            return [start, nodes[0]];
        }

        var route = new List<uint> { start };
        var unvisited = new HashSet<uint>(nodes.Where(n => n != start));

        while (unvisited.Count > 0)
        {
            var bestInsertionCost = float.MaxValue;
            var bestIndex = -1;
            uint bestNode = 0;

            foreach (var nodeToInsert in unvisited)
            {
                for (var i = 0; i < route.Count; i++)
                {
                    var from = route[i];
                    if (i == route.Count - 1)
                    {
                        var costAtEnd = graph[from][nodeToInsert].Cost;
                        if (costAtEnd < bestInsertionCost)
                        {
                            bestInsertionCost = costAtEnd;
                            bestIndex = route.Count;
                            bestNode = nodeToInsert;
                        }
                    }
                    else
                    {
                        var to = route[i + 1];
                        var addedCost = graph[from][nodeToInsert].Cost + graph[nodeToInsert][to].Cost - graph[from][to].Cost;
                        if (addedCost < bestInsertionCost)
                        {
                            bestInsertionCost = addedCost;
                            bestIndex = i + 1;
                            bestNode = nodeToInsert;
                        }
                    }
                }
            }

            if (bestIndex == -1)
            {
                break;
            }

            route.Insert(bestIndex, bestNode);
            unvisited.Remove(bestNode);
        }

        return route;
    }

    private List<HuntPathfinderStep> BuildStepPath(
        List<uint> orderedNodes,
        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph
    )
    {
        List<HuntPathfinderStep> steps = [];

        for (var i = 0; i < orderedNodes.Count - 1; i++)
        {
            var from = orderedNodes[i];
            var to = orderedNodes[i + 1];
            if (graph.TryGetValue(from, out var edges) && edges.TryGetValue(to, out var segment))
            {
                steps.AddRange(segment.Steps);
            }
        }

        return steps;
    }

    private void PrintPath(List<HuntPathfinderStep> steps)
    {
        log.Info("== Treasure Hunt Steps ==");

        var index = 1;
        var treasureCount = 0;

        foreach (var step in steps)
        {
            var message = step.Type switch
            {
                HuntPathfinderStepType.WalkToNode => $"[{index}] Walk to Destination: {step.NodeId}",
                HuntPathfinderStepType.WalkToAethernet => $"[{index}] Walk to Aethernet: {step.Aethernet}",
                HuntPathfinderStepType.TeleportToAethernet => $"[{index}] Teleport to Aethernet: {step.Aethernet}",
                HuntPathfinderStepType.ReturnToBaseCamp => $"[{index}] Return to Base Camp",
                _ => $"[{index}] Unknown Step Type",
            };

            if (step.Type == HuntPathfinderStepType.WalkToNode)
            {
                treasureCount++;
            }

            log.Info(message);
            index++;
        }

        log.Info($"== Total treasures visited: {treasureCount} ==");
    }
}
