using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Zones.Graph.Traversal;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Paths;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Automator.Services.Paths;

public class PathCalculator(
    IPathfinder pathfinder,
    IObjectTable objects,
    IZoneProvider zones,
    ILogger<PathCalculator> logger
) : IPathCalculator
{
    public async Task<Queue<IPathStep>> Calculate(IGoal goal)
    {
        if (objects.LocalPlayer is not { } player)
        {
            logger.Warn("No Player");
            return [];
        }

        var zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            logger.Warn("In wrong zone");
            return [];
        }

        var graph = await zone.GetGraph();

        Node goalNode;
        try
        {
            goalNode = GetGoalNode(goal, graph);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            logger.Error(ex.Message);
            return [];
        }

        var destination = goalNode.Position;

        if (player.Position.Distance2D(destination) <= 20f)
        {
            logger.Debug("Too close to destination.");
            return [];
        }

        var traverser = new GraphTraverser(graph, pathfinder, logger);
        traverser.AddCalculator(new DirectWalkCalculator());
        traverser.AddCalculator(new WalkTeleportWalkCalculator());
        traverser.AddCalculator(new ReturnWalkCalculator());
        traverser.AddCalculator(new ReturnTeleportWalkCalculator());

        var steps = await traverser.FindPath(player.Position, goalNode);
        var resolvedSteps = steps
            .Select(step => AethernetNavigation.ResolveAetherytePathStep(step, zone))
            .ToList();

        return new Queue<IPathStep>(resolvedSteps);
    }

    private Node GetGoalNode(IGoal goal, ZoneGraph graph)
    {
        return goal.GoalType switch
        {
            CriticalEncounterGoal(var id) => GetActivityNode(id.Value, graph, NodeType.CriticalEncounter),
            FateGoal(var id) => GetActivityNode(id.Value, graph, NodeType.NormalFate, NodeType.PotFate),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private Node GetActivityNode(int id, ZoneGraph graph, params NodeType[] types)
    {
        var nodes = graph.GetNodesByTypes(types).Where(n =>
        {
            if (n.Metadata is not ActivityNodeMetadata meta)
            {
                return false;
            }

            return meta.Id == id;
        }).ToList();

        return nodes.Count == 0 ? throw new Exception("No nodes for Activity") : nodes.First();
    }
}
