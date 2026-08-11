using BOCCHI.Common.Data.Zones;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Text.Json;

namespace BOCCHI.Treasure.Hunt;

public interface IHuntRoutePlanner
{
    HuntPathfinderState State { get; }

    /// <summary>Half chosen as primary on the last authored FindPath (red/blue), if any.</summary>
    string? LastPrimaryHalf { get; }

    string? TryGetNodeHalf(uint nodeId);

    /// <summary>Authored route order index (0-based), or null if not in treasure_route.json.</summary>
    int? TryGetNodeOrderIndex(uint nodeId);

    bool HalfHasRemaining(string half, IReadOnlyList<uint> remainingNodes);

    /// <summary>Other tagged half that still has remaining pads, if any.</summary>
    string? TryGetAlternateHalf(string half, IReadOnlyList<uint> remainingNodes);

    /// <param name="preferStartNodes">
    ///     When set, visit these remaining pads first (closest Nearby peel-off chain), then the rest.
    /// </param>
    /// <param name="stickyHalf">
    ///     When set (e.g. red/blue), finish that half before the other — survives empty skips.
    /// </param>
    Task<List<HuntPathfinderStep>> FindPath(
        Vector3 start,
        List<uint> nodes,
        IReadOnlyList<uint>? preferStartNodes = null,
        string? stickyHalf = null);
}

/// <summary>
///     Routes remaining coffers via authored treasure_route.json (v2) when present; otherwise
///     open-path nearest-neighbor TSP. Re-solved on every FindPath.
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

    /// <summary>
    ///     Yalm-equivalent cost for casting Return (aligned with Illegal Mode's ReturnCost).
    /// </summary>
    public const float ReturnCost = 40f;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private HuntNodeDataSchema data = new();

    /// <summary>Flattened authored pads in order; empty when falling back to TSP.</summary>
    private List<AuthoredRouteEntry> authoredEntries = [];

    /// <summary>Half chosen as primary on the last authored FindPath (red/blue), if any.</summary>
    public string? LastPrimaryHalf { get; private set; }

    private HuntAethernet BaseCampAethernet => zoneId switch
    {
        ZoneId.NorthHorn => HuntAethernet.NorthHornBaseCamp,
        _ => HuntAethernet.BaseCamp,
    };

    public HuntPathfinderState State { get; private set; } = HuntPathfinderState.None;

    public Task<List<HuntPathfinderStep>> FindPath(
        Vector3 start,
        List<uint> nodes,
        IReadOnlyList<uint>? preferStartNodes = null,
        string? stickyHalf = null)
    {
        if (State != HuntPathfinderState.FileLoaded && State != HuntPathfinderState.PathfindingDone)
        {
            throw new InvalidOperationException("Hunt route data not loaded");
        }

        State = HuntPathfinderState.Pathfinding;
        LastPrimaryHalf = null;

        List<uint> remaining = nodes.Distinct().ToList();
        if (remaining.Count == 0)
        {
            State = HuntPathfinderState.PathfindingDone;
            return Task.FromResult(new List<HuntPathfinderStep>());
        }

        List<uint> preferPrefix = [];
        if (preferStartNodes != null)
        {
            string? stickyNorm = NormalizeHalf(stickyHalf);
            HashSet<uint> seen = [];
            foreach (uint id in preferStartNodes)
            {
                if (!remaining.Contains(id) || !seen.Add(id))
                {
                    continue;
                }

                // While a half is sticky, only peel to lives on that half.
                if (stickyNorm != null)
                {
                    string? idHalf = TryGetNodeHalf(id);
                    if (idHalf != stickyNorm)
                    {
                        continue;
                    }
                }

                preferPrefix.Add(id);
            }
        }

        uint? primaryPrefer = preferPrefix.Count > 0 ? preferPrefix[0] : null;
        List<uint> tour = authoredEntries.Count > 0
            ? BuildAuthoredTour(start, remaining, primaryPrefer, stickyHalf)
            : BuildTspTour(start, remaining, primaryPrefer);

        if (preferPrefix.Count > 0)
        {
            // Closest Nearby chain first, then the rest of the authored/TSP tour.
            HashSet<uint> prefixSet = preferPrefix.ToHashSet();
            tour = preferPrefix.Concat(tour.Where(id => !prefixSet.Contains(id))).ToList();
        }

        if (tour.Count == 0)
        {
            State = HuntPathfinderState.PathfindingDone;
            return Task.FromResult(new List<HuntPathfinderStep>());
        }

        log.Info(
            authoredEntries.Count > 0
                ? "Treasure hunt authored route: {Count} remaining (start {Start}, nearbyPrefix {Prefix}, half {Half})"
                : "Treasure hunt nearest-neighbor route: {Count} remaining (start {Start}, nearbyPrefix {Prefix})",
            tour.Count,
            tour[0],
            preferPrefix.Count,
            LastPrimaryHalf ?? stickyHalf ?? "-");

        List<HuntPathfinderStep> steps = BuildStepsForTour(tour);
        State = HuntPathfinderState.PathfindingDone;
        return Task.FromResult(steps);
    }

    public string? TryGetNodeHalf(uint nodeId)
    {
        foreach (AuthoredRouteEntry entry in authoredEntries)
        {
            if (entry.NodeId == nodeId)
            {
                return entry.Half;
            }
        }

        return null;
    }

    public int? TryGetNodeOrderIndex(uint nodeId)
    {
        for (int i = 0; i < authoredEntries.Count; i++)
        {
            if (authoredEntries[i].NodeId == nodeId)
            {
                return i;
            }
        }

        return null;
    }

    public bool HalfHasRemaining(string half, IReadOnlyList<uint> remainingNodes)
    {
        string normalized = NormalizeHalf(half) ?? half;
        HashSet<uint> remaining = remainingNodes.ToHashSet();
        return authoredEntries.Any(e => e.Half == normalized && remaining.Contains(e.NodeId));
    }

    public string? TryGetAlternateHalf(string half, IReadOnlyList<uint> remainingNodes)
    {
        string? normalized = NormalizeHalf(half);
        HashSet<uint> remaining = remainingNodes.ToHashSet();
        foreach (string? candidate in authoredEntries.Select(e => e.Half).Distinct())
        {
            if (candidate == null || candidate == normalized)
            {
                continue;
            }

            if (authoredEntries.Any(e => e.Half == candidate && remaining.Contains(e.NodeId)))
            {
                return candidate;
            }
        }

        return null;
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
        LoadAuthoredRoute();
        State = HuntPathfinderState.FileLoaded;
    }

    private void LoadAuthoredRoute()
    {
        authoredEntries = [];
        string file = GetDataFile(plugin, zoneId, "treasure_route.json");
        if (!File.Exists(file))
        {
            log.Info("No treasure_route.json for {Zone}; using nearest-neighbor TSP", zoneId);
            return;
        }

        try
        {
            string json = File.ReadAllText(file);
            AuthoredTreasureRoute? route = JsonSerializer.Deserialize<AuthoredTreasureRoute>(json, JsonOptions);
            if (route is not { SchemaVersion: >= 2 } || route.Segments.Count == 0)
            {
                log.Info(
                    "treasure_route.json for {Zone} is not schema v2 with segments; using TSP",
                    zoneId);
                return;
            }

            foreach (AuthoredTreasureSegment segment in route.Segments)
            {
                if (segment.Nodes.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < segment.Nodes.Count; i++)
                {
                    bool lastInSegment = i == segment.Nodes.Count - 1;
                    authoredEntries.Add(new AuthoredRouteEntry(
                        segment.Nodes[i],
                        NormalizeHalf(segment.Half),
                        lastInSegment ? segment.TransitionAfter : null));
                }
            }

            log.Info(
                "Loaded authored treasure route for {Zone}: {Pads} pads in {Segments} segment(s)",
                zoneId,
                authoredEntries.Count,
                route.Segments.Count);
        }
        catch (Exception ex)
        {
            authoredEntries = [];
            log.Warning(ex, "Failed to load treasure_route.json for {Zone}; using TSP", zoneId);
        }
    }

    private static string? NormalizeHalf(string? half)
    {
        if (string.IsNullOrWhiteSpace(half))
        {
            return null;
        }

        return half.Trim().ToLowerInvariant();
    }

    private List<uint> BuildTspTour(Vector3 start, List<uint> remaining, uint? preferStartNode)
    {
        uint startNode = preferStartNode is uint preferred && remaining.Contains(preferred)
            ? preferred
            : remaining
                .OrderBy(id => Vector3.DistanceSquared(start, GetNodePosition(id)))
                .First();

        Dictionary<uint, Dictionary<uint, (float Cost, List<HuntPathfinderStep> Steps)>> graph =
            BuildCostGraph(remaining);
        return SolveTspNearestNeighbor(startNode, remaining, graph);
    }

    private List<uint> BuildAuthoredTour(
        Vector3 start,
        List<uint> remaining,
        uint? preferStartNode,
        string? stickyHalf)
    {
        HashSet<uint> remainingSet = remaining.ToHashSet();
        Dictionary<uint, int> orderIndex = [];
        List<AuthoredRouteEntry> orderedUnique = [];
        for (int i = 0; i < authoredEntries.Count; i++)
        {
            AuthoredRouteEntry entry = authoredEntries[i];
            if (!remainingSet.Contains(entry.NodeId) || orderIndex.ContainsKey(entry.NodeId))
            {
                continue;
            }

            orderIndex[entry.NodeId] = orderedUnique.Count;
            orderedUnique.Add(entry);
        }

        // Pads in remaining but missing from authored file — append by distance from last.
        foreach (uint id in remaining.Where(id => !orderIndex.ContainsKey(id)))
        {
            orderIndex[id] = orderedUnique.Count;
            orderedUnique.Add(new AuthoredRouteEntry(id, null, null));
        }

        if (orderedUnique.Count == 0)
        {
            return [];
        }

        if (preferStartNode is uint prefer && orderIndex.ContainsKey(prefer))
        {
            List<uint> baseTour = BuildHalfAwareTour(start, orderedUnique, stickyHalf);
            baseTour.Remove(prefer);
            baseTour.Insert(0, prefer);
            return baseTour;
        }

        return BuildHalfAwareTour(start, orderedUnique, stickyHalf);
    }

    private List<uint> BuildHalfAwareTour(
        Vector3 start,
        List<AuthoredRouteEntry> orderedUnique,
        string? stickyHalf)
    {
        List<string> halves = orderedUnique
            .Select(e => e.Half)
            .Where(h => h != null)
            .Cast<string>()
            .Distinct()
            .ToList();

        if (halves.Count >= 2)
        {
            string? normalizedSticky = NormalizeHalf(stickyHalf);
            // Sticky wins; otherwise keep authored segment order (South: red before blue)
            // so camp starts on red instead of whichever half is geographically closer.
            string firstHalf = normalizedSticky != null
                               && halves.Contains(normalizedSticky)
                               && orderedUnique.Any(e => e.Half == normalizedSticky)
                ? normalizedSticky
                : halves[0];
            string otherHalf = halves.First(h => h != firstHalf);
            LastPrimaryHalf = firstHalf;

            List<uint> tour = orderedUnique
                .Where(e => e.Half == firstHalf)
                .Select(e => e.NodeId)
                .ToList();

            // Sticky half: only that half this plan. The hunter switches sticky + Returns
            // when the half is exhausted — do not append the other half into the same tour
            // (that caused mid-route turnarounds into blue while still "on red").
            if (normalizedSticky == null)
            {
                tour.AddRange(orderedUnique
                    .Where(e => e.Half == otherHalf)
                    .Select(e => e.NodeId));
                tour.AddRange(orderedUnique
                    .Where(e => e.Half == null)
                    .Select(e => e.NodeId));
            }

            return tour;
        }

        // Sticky half: only that color, even when the remaining set only has one half loaded.
        // Returning the other half here used to flip camp starts (red → first blue).
        if (NormalizeHalf(stickyHalf) is string stickyNorm)
        {
            LastPrimaryHalf = stickyNorm;
            return orderedUnique
                .Where(e => e.Half == stickyNorm)
                .Select(e => e.NodeId)
                .ToList();
        }

        // Single / no half: enter at nearest remaining, continue authored order with wrap.
        uint entry = orderedUnique
            .OrderBy(e => Vector3.DistanceSquared(start, GetNodePosition(e.NodeId)))
            .First()
            .NodeId;
        if (halves.Count == 1)
        {
            LastPrimaryHalf = halves[0];
        }

        return OrderFromEntry(orderedUnique, entry);
    }

    private static List<uint> OrderFromEntry(List<AuthoredRouteEntry> orderedUnique, uint entry)
    {
        int idx = orderedUnique.FindIndex(e => e.NodeId == entry);
        if (idx < 0)
        {
            return orderedUnique.Select(e => e.NodeId).ToList();
        }

        List<uint> tour = [];
        for (int i = 0; i < orderedUnique.Count; i++)
        {
            tour.Add(orderedUnique[(idx + i) % orderedUnique.Count].NodeId);
        }

        return tour;
    }

    private List<HuntPathfinderStep> BuildStepsForTour(List<uint> tour)
    {
        List<HuntPathfinderStep> steps = [HuntPathfinderStep.WalkToDestination(tour[0])];
        for (int i = 0; i < tour.Count - 1; i++)
        {
            uint from = tour[i];
            uint to = tour[i + 1];
            AuthoredTreasureTransition? transition = FindTransitionBetween(from, to);
            steps.AddRange(ResolveLeg(from, to, transition));
        }

        return steps;
    }

    private AuthoredTreasureTransition? FindTransitionBetween(uint from, uint to)
    {
        string? fromHalf = TryGetNodeHalf(from);
        string? toHalf = TryGetNodeHalf(to);

        // Crossing halves always Returns (unless an explicit teleport is authored on the boundary).
        if (fromHalf != null && toHalf != null && fromHalf != toHalf)
        {
            for (int i = 0; i < authoredEntries.Count; i++)
            {
                AuthoredRouteEntry entry = authoredEntries[i];
                if (entry.NodeId != from || entry.TransitionAfter == null)
                {
                    continue;
                }

                string boundaryType = entry.TransitionAfter.Type?.Trim().ToLowerInvariant() ?? "";
                if (boundaryType is "teleport" or "return")
                {
                    return entry.TransitionAfter;
                }
            }

            return new AuthoredTreasureTransition { Type = "return" };
        }

        // Same half (or untagged): walk only — never Return/TP mid-half.
        return new AuthoredTreasureTransition { Type = "walk" };
    }

    private List<HuntPathfinderStep> ResolveLeg(uint fromId, uint toId, AuthoredTreasureTransition? transition)
    {
        string type = transition?.Type?.Trim().ToLowerInvariant() ?? "walk";
        switch (type)
        {
            case "return":
                return
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.WalkToDestination(toId)
                ];
            case "teleport" when TryParseAethernet(transition?.To, out HuntAethernet shard):
                return
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.TeleportToAethernet(shard),
                    HuntPathfinderStep.WalkToDestination(toId)
                ];
            case "none":
            case "walk":
                return [HuntPathfinderStep.WalkToDestination(toId)];
            default:
                // auto — within a half prefer walk; otherwise cheapest bake hop.
                string? fromHalf = TryGetNodeHalf(fromId);
                string? toHalf = TryGetNodeHalf(toId);
                if (fromHalf != null && fromHalf == toHalf)
                {
                    return [HuntPathfinderStep.WalkToDestination(toId)];
                }

                if (TryGetWalkSteps(fromId, toId, out List<HuntPathfinderStep> walk))
                {
                    return walk;
                }

                return GetBestSteps(fromId, toId).Steps;
        }
    }

    private bool TryGetWalkSteps(uint fromId, uint toId, out List<HuntPathfinderStep> steps)
    {
        steps = [];
        if (data.NodeToNodeDistances.TryGetValue(fromId, out List<HuntToNode>? directList))
        {
            HuntToNode direct = directList.FirstOrDefault(x => x.Id == toId);
            if (direct.Id == toId)
            {
                steps = [HuntPathfinderStep.WalkToDestination(toId)];
                return true;
            }
        }

        return false;
    }

    private static bool TryParseAethernet(string? name, out HuntAethernet aethernet)
    {
        aethernet = default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return Enum.TryParse(name.Trim(), ignoreCase: true, out aethernet);
    }

    /// <summary>Cheapest of walk, aethernet hop, or Return (+ optional aethernet from camp).</summary>
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

        HuntAethernet baseCamp = BaseCampAethernet;
        if (data.AethernetToNodeDistances.TryGetValue(baseCamp, out List<HuntToNode>? fromBaseList))
        {
            HuntToNode walkFromBase = fromBaseList.FirstOrDefault(x => x.Id == toId);
            if (walkFromBase.Id == toId)
            {
                float returnWalkCost = ReturnCost + walkFromBase.Distance;
                if (returnWalkCost < bestCost)
                {
                    bestCost = returnWalkCost;
                    bestSteps =
                    [
                        HuntPathfinderStep.ReturnToBaseCamp(),
                        HuntPathfinderStep.WalkToDestination(toId)
                    ];
                }
            }
        }

        // Return lands at base camp — optional aethernet hop from there to a closer shard.
        foreach ((HuntAethernet aethernet, List<HuntToNode> list) in data.AethernetToNodeDistances)
        {
            if (aethernet == baseCamp)
            {
                continue;
            }

            HuntToNode to = list.FirstOrDefault(x => x.Id == toId);
            if (to.Id != toId)
            {
                continue;
            }

            float cost = ReturnCost + AethernetHopCost + to.Distance;
            if (cost < bestCost)
            {
                bestCost = cost;
                bestSteps =
                [
                    HuntPathfinderStep.ReturnToBaseCamp(),
                    HuntPathfinderStep.TeleportToAethernet(aethernet),
                    HuntPathfinderStep.WalkToDestination(toId)
                ];
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
    ///     Nearest-neighbor TSP: from the current node, always append the cheapest unvisited neighbor.
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

    private static string GetDataFile(IDalamudPluginInterface plugin, ZoneId zoneId, string filename)
    {
        string pluginDir = GetPluginDirectory(plugin);
        return Path.Combine(pluginDir, "Data", zoneId.TreasureDataFolder(), filename);
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
