using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Fates;
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
using System.Numerics;

namespace BOCCHI.Automator.Services.Paths;

public class PathCalculator
(
    IPathfinder pathfinder,
    IObjectTable objects,
    IZoneProvider zones,
    IFateRepository fates,
    IFateContext fateContext,
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

        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            logger.Warn("In wrong zone");
            return [];
        }

        // Already registered in the target FATE — no more travel needed.
        if (goal.GoalType is FateGoal fateGoal && fateContext.GetFateId() == fateGoal.id)
        {
            logger.Debug("Already inside target FATE.");
            return [];
        }

        ZoneGraph graph = await zone.GetGraph();

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

        // Prefer live FATE center when available — authored graph points can sit outside the circle.
        Node pathGoal = goalNode;
        if (goal.GoalType is FateGoal liveFateGoal
            && fates.Snapshot().FirstOrDefault(f => f.Id.Value == liveFateGoal.id.Value) is { } liveFate)
        {
            pathGoal = new Node
            {
                Type = goalNode.Type,
                Position = liveFate.Position,
                Metadata = goalNode.Metadata
            };
        }

        if (player.Position.Distance2D(pathGoal.Position) <= NavigationConstants.EventArrivalRadius)
        {
            logger.Debug("Too close to destination.");
            return [];
        }

        GraphTraverser traverser = new(graph, pathfinder, logger);
        // Teleport-first: from camp this is usually instant (no vnav). DirectWalk only for short hops.
        traverser.AddCalculator(new WalkTeleportWalkCalculator());
        traverser.AddCalculator(new DirectWalkCalculator());
        traverser.AddCalculator(new ReturnWalkCalculator());
        traverser.AddCalculator(new ReturnTeleportWalkCalculator());

        List<PathStep> steps = await traverser.FindPath(player.Position, pathGoal);
        List<PathStep> resolvedSteps = steps
            .Select(step => AethernetNavigation.ResolveAetherytePathStep(step, zone))
            .ToList();

        return new(resolvedSteps);
    }

    private Node GetGoalNode(IGoal goal, ZoneGraph graph)
    {
        return goal.GoalType switch
        {
            CriticalEncounterGoal(var id) => GetActivityNode(id.Value, graph, NodeType.CriticalEncounter),
            FateGoal(var id) => GetActivityNode(id.Value, graph, NodeType.NormalFate, NodeType.PotFate),
            var _ => throw new ArgumentOutOfRangeException()
        };
    }

    private Node GetActivityNode(int id, ZoneGraph graph, params NodeType[] types)
    {
        List<Node> nodes = graph.GetNodesByTypes(types).Where(n =>
        {
            if (n.Metadata is not ActivityNodeMetadata meta)
            {
                return false;
            }

            return meta.Id == id;
        }).ToList();

        return nodes.Count == 0 ? throw new("No nodes for Activity") : nodes.First();
    }
}
