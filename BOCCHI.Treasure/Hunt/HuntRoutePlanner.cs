using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Text.Json;

namespace BOCCHI.Treasure.Hunt;

public interface IHuntRoutePlanner
{
    HuntPathfinderState State { get; }

    /// <param name="preferStartNode">
    ///     When set and still remaining, start the tour there (e.g. a live coffer next to the player).
    /// </param>
    Task<List<HuntPathfinderStep>> FindPath(Vector3 start, List<uint> nodes, uint? preferStartNode = null);
}

/// <summary>
///     Open-path TSP via nearest-neighbor (W3Schools greedy): from the player / preferred
///     coffer, always visit the cheapest remaining node next. Re-solved on every FindPath.
/// </summary>
public abstract class HuntRoutePlanner
(
    ZoneId zoneId,
    IDalamudPluginInterface plugin,
    IPluginLog log
) : IHuntRoutePlanner
{
    /// <summary>
    ///     Extra yalm-equivalent cost for an aethernet hop when comparing walk vs teleport routes.
    /// </summary>
    public const float AethernetHopCost = 50f;

    private HuntNodeDataSchema data = new();

    public HuntPathfinderState State { get; private set; } = HuntPathfinderState.None;

    public Task<List<HuntPathfinderStep>> FindPath(Vector3 start, List<uint> nodes, uint? preferStartNode = null)
    {
        if (State != HuntPathfinderState.FileLoaded && State != HuntPathfinderState.PathfindingDone)
        {
            throw new InvalidOperationException("Hunt route data not loaded");
        }

        State = HuntPathfinderState.Pathfinding;

        List<uint> remaining = nodes.Distinct().ToList();
        if (remaining.Count == 0)
        {
            State = HuntPathfinderState.PathfindingDone;
            return Task.FromResult(new List<HuntPathfinderStep>());
        }

        uint startNode = preferStartNode is uint preferred && remaining.Contains(preferred)
            ? preferred
            : remaining
                .OrderBy(id => Vector3.DistanceSquared(start, GetNodePosition(id)))
                .First();

        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph =
            BuildCostGraph(remaining);
        List<uint> tour = SolveTspNearestNeighbor(startNode, remaining, graph);

        log.Info(
            "Treasure hunt nearest-neighbor route: {Count} remaining coffers (start {Start})",
            tour.Count,
            startNode);

        List<HuntPathfinderStep> steps = [HuntPathfinderStep.WalkToDestination(tour[0])];
        for (int i = 0; i < tour.Count - 1; i++)
        {
            (float _, List<HuntPathfinderStep> segment) = GetBestSteps(tour[i], tour[i + 1]);
            steps.AddRange(segment);
        }

        PrintPath(steps);
        State = HuntPathfinderState.PathfindingDone;
        return Task.FromResult(steps);
    }

    protected abstract Vector3 GetNodePosition(uint nodeId);

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

    /// <summary>Walk or aethernet only — never mid-route Return.</summary>
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
            foreach ((HuntAethernet aethernet, List<HuntToNode> list) in data.AethernetToNodeDistances)
            {
                HuntToNode to = list.FirstOrDefault(x => x.Id == toId);
                if (to.Id != toId)
                {
                    continue;
                }

                float cost = fromShard.Distance + AethernetHopCost + to.Distance;
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

        if (bestSteps.Count == 0)
        {
            // Fallback when bake data is missing a pair — walk via destination id only.
            bestCost = Vector3.Distance(GetNodePosition(fromId), GetNodePosition(toId));
            bestSteps = [HuntPathfinderStep.WalkToDestination(toId)];
        }

        return (bestCost, bestSteps);
    }

    private Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> BuildCostGraph(List<uint> nodes)
    {
        Dictionary<uint, Dictionary<uint, (float, List<HuntPathfinderStep>)>> graph = new();

        foreach (uint from in nodes)
        {
            graph[from] = new();
            foreach (uint to in nodes)
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

    /// <summary>
    ///     W3Schools greedy TSP: from the current node, always append the nearest unvisited neighbor.
    ///     Open path — does not return to the start.
    /// </summary>
    private static List<uint> SolveTspNearestNeighbor(
        uint start,
        List<uint> nodes,
        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph
    )
    {
        if (nodes.Count == 0)
        {
            return [];
        }

        if (nodes.Count == 1)
        {
            return [start];
        }

        List<uint> route = [start];
        HashSet<uint> unvisited = new(nodes.Where(n => n != start));

        while (unvisited.Count > 0)
        {
            uint last = route[^1];
            uint? nearest = null;
            float minCost = float.MaxValue;

            foreach (uint candidate in unvisited)
            {
                float cost = graph[last][candidate].Cost;
                if (cost < minCost)
                {
                    minCost = cost;
                    nearest = candidate;
                }
            }

            if (nearest is not uint next)
            {
                break;
            }

            route.Add(next);
            unvisited.Remove(next);
        }

        return route;
    }

    private void PrintPath(List<HuntPathfinderStep> steps)
    {
        log.Info("== Treasure Hunt Steps ==");

        int index = 1;
        int treasureCount = 0;

        foreach (HuntPathfinderStep step in steps)
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
            ZoneId.NorthHorn => "NorthHorn",
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
