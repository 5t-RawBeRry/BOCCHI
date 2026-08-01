using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Text.Json;

namespace BOCCHI.Treasure.Hunt;

public interface IHuntRoutePlanner
{
    HuntPathfinderState State { get; }

    Task<List<HuntPathfinderStep>> FindPath(Vector3 start, List<uint> nodes);
}

public abstract class HuntRoutePlanner
(
    ZoneId zoneId,
    IDalamudPluginInterface plugin,
    IPluginLog log,
    float returnCost = 300f,
    float teleportCost = 50f
) : IHuntRoutePlanner
{
    private HuntNodeDataSchema data = new();
    public HuntPathfinderState State { get; private set; } = HuntPathfinderState.None;

    public Task<List<HuntPathfinderStep>> FindPath(Vector3 start, List<uint> nodes)
    {
        if (State != HuntPathfinderState.FileLoaded)
        {
            throw new InvalidOperationException("Hunt route data not loaded");
        }

        State = HuntPathfinderState.Pathfinding;

        uint startNode = GetStartingNode(start, nodes);
        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph = BuildCostGraph(nodes);
        List<uint> ordered = SolveTspNearestInsertion(startNode, nodes, graph);
        List<HuntPathfinderStep> steps = BuildStepPath(ordered, graph);

        steps.Insert(0, HuntPathfinderStep.WalkToDestination(startNode));

        PrintPath(steps);
        State = HuntPathfinderState.PathfindingDone;
        return Task.FromResult(steps);
    }

    protected abstract uint GetStartingNode(Vector3 start, List<uint> nodes);

    protected void LoadFile(string filename)
    {
        State = HuntPathfinderState.LoadingFile;

        string file = GetDataFile(plugin, zoneId, filename);
        if (!File.Exists(file))
        {
            log.Error($"Required hunt data file not found: {file}");
            return;
        }

        string json = File.ReadAllText(file);
        data = JsonSerializer.Deserialize<HuntNodeDataSchema>(json) ?? new HuntNodeDataSchema();
        State = HuntPathfinderState.FileLoaded;
    }

    protected (float Cost, List<HuntPathfinderStep> Steps) GetBestSteps(uint fromId, uint toId)
    {
        float bestCost = float.MaxValue;
        List<HuntPathfinderStep> bestSteps = [];

        if (data.NodeToNodeDistances.TryGetValue(fromId, out List<HuntToNode>? directList))
        {
            HuntToNode direct = directList.FirstOrDefault(x => x.Id == toId);
            if (direct.Id == toId)
            {
                bestCost = direct.Distance;
                bestSteps = [HuntPathfinderStep.WalkToDestination(toId)];
            }
        }

        if (data.NodeToAethernetDistances.TryGetValue(fromId, out List<HuntToAethernet>? shardList) && shardList.Count > 0)
        {
            HuntToAethernet fromShard = shardList.OrderBy(x => x.Distance).First();
            foreach((HuntAethernet aethernet, List<HuntToNode> list) in data.AethernetToNodeDistances)
            {
                HuntToNode to = list.FirstOrDefault(x => x.Id == toId);
                if (to.Id != toId)
                {
                    continue;
                }

                float cost = fromShard.Distance + teleportCost + to.Distance;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSteps =
                    [
                        HuntPathfinderStep.WalkToAethernet(fromShard.Aethernet),
                        HuntPathfinderStep.TeleportToAethernet(aethernet),
                        HuntPathfinderStep.WalkToDestination(toId)
                    ];
                }
            }
        }

        foreach((HuntAethernet aethernet, List<HuntToNode> list) in data.AethernetToNodeDistances)
        {
            HuntToNode to = list.FirstOrDefault(x => x.Id == toId);
            if (to.Id != toId)
            {
                continue;
            }

            float cost = returnCost + teleportCost + to.Distance;
            if (cost >= bestCost)
            {
                continue;
            }

            bestCost = cost;
            bestSteps = aethernet == HuntAethernet.BaseCamp
                ?
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.WalkToDestination(toId)
                ]
                :
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.TeleportToAethernet(aethernet),
                    HuntPathfinderStep.WalkToDestination(toId)
                ];
        }

        return (bestCost, bestSteps);
    }

    private Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> BuildCostGraph(List<uint> nodes)
    {
        Dictionary<uint, Dictionary<uint, (float, List<HuntPathfinderStep>)>> graph = new();

        foreach(uint from in nodes)
        {
            graph[from] = new();
            foreach(uint to in nodes)
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

        List<uint> route = new()
            { start };
        HashSet<uint> unvisited = new(nodes.Where(n => n != start));

        while(unvisited.Count > 0)
        {
            float bestInsertionCost = float.MaxValue;
            int bestIndex = -1;
            uint bestNode = 0;

            foreach(uint nodeToInsert in unvisited)
            {
                for(int i = 0; i < route.Count; i++)
                {
                    uint from = route[i];
                    if (i == route.Count - 1)
                    {
                        float costAtEnd = graph[from][nodeToInsert].Cost;
                        if (costAtEnd < bestInsertionCost)
                        {
                            bestInsertionCost = costAtEnd;
                            bestIndex = route.Count;
                            bestNode = nodeToInsert;
                        }
                    }
                    else
                    {
                        uint to = route[i + 1];
                        float addedCost = graph[from][nodeToInsert].Cost + graph[nodeToInsert][to].Cost - graph[from][to].Cost;
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

        for(int i = 0; i < orderedNodes.Count - 1; i++)
        {
            uint from = orderedNodes[i];
            uint to = orderedNodes[i + 1];
            if (graph.TryGetValue(from, out Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>? edges) && edges.TryGetValue(to, out (float Cost, List<HuntPathfinderStep> Steps) segment))
            {
                steps.AddRange(segment.Steps);
            }
        }

        return steps;
    }

    private void PrintPath(List<HuntPathfinderStep> steps)
    {
        log.Info("== Treasure Hunt Steps ==");

        int index = 1;
        int treasureCount = 0;

        foreach(HuntPathfinderStep step in steps)
        {
            string message = step.Type switch
            {
                HuntPathfinderStepType.WalkToNode => $"[{index}] Walk to Destination: {step.NodeId}",
                HuntPathfinderStepType.WalkToAethernet => $"[{index}] Walk to Aethernet: {step.Aethernet}",
                HuntPathfinderStepType.TeleportToAethernet => $"[{index}] Teleport to Aethernet: {step.Aethernet}",
                HuntPathfinderStepType.ReturnToBaseCamp => $"[{index}] Return to Base Camp",
                var _ => $"[{index}] Unknown Step Type"
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

    private static string GetDataFile(IDalamudPluginInterface plugin, ZoneId zoneId, string filename)
    {
        string zoneFolder = zoneId switch
        {
            ZoneId.SouthHorn => "SouthHorn",
            var _ => throw new NotSupportedException($"No hunt data for zone {zoneId}")
        };

        string pluginDir = GetPluginDirectory(plugin);
        return Path.Combine(pluginDir, "Data", zoneFolder, filename);
    }

    private static string GetPluginDirectory(IDalamudPluginInterface plugin)
    {
        string? pluginDir = plugin.AssemblyLocation.DirectoryName;
        if (!string.IsNullOrEmpty(pluginDir))
        {
            return pluginDir;
        }

        string? assemblyDir = Path.GetDirectoryName(plugin.GetType().Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            return assemblyDir;
        }

        throw new InvalidOperationException("Unable to resolve the BOCCHI plugin directory for hunt data files.");
    }
}
