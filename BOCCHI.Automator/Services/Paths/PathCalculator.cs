using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
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
    ICriticalEncounterRepository criticalEncounters,
    IFateContext fateContext,
    AutomatorConfig config,
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

        // Prefer live FATE/CE center when available — authored graph points can sit outside the circle.
        Node pathGoal = goalNode;
        float arrivalRadius = NavigationConstants.EventArrivalRadius;
        if (goal.GoalType is FateGoal liveFateGoal
            && fates.Snapshot().FirstOrDefault(f => f.Id.Value == liveFateGoal.id.Value) is { } liveFate)
        {
            pathGoal = new Node
            {
                Id = goalNode.Id,
                Type = goalNode.Type,
                Position = liveFate.Position,
                Metadata = goalNode.Metadata
            };

            // Path toward the FATE center — do not treat "inside the yellow circle" (~20y+) as arrived.
            // InFateHandler continues into max melee of the boss once CurrentFate registers.
            arrivalRadius = NavigationConstants.EventArrivalRadius;
        }
        else if (goal.GoalType is CriticalEncounterGoal liveCeGoal
                 && criticalEncounters.SnapshotWithoutForkedTower()
                     .FirstOrDefault(c => c.Id.Value == liveCeGoal.id.Value) is { } liveCe
                 && !float.IsNaN(liveCe.Position.X))
        {
            pathGoal = new Node
            {
                Id = goalNode.Id,
                Type = goalNode.Type,
                Position = liveCe.Position,
                Metadata = goalNode.Metadata
            };

            float combatRadius = liveCe.Radius - NavigationConstants.CriticalEncounterRadiusPadding;
            if (combatRadius > 0f)
            {
                arrivalRadius = Math.Max(arrivalRadius, combatRadius * 0.85f);
            }
        }

        float distanceToGoal = player.Position.Distance2D(pathGoal.Position);
        if (distanceToGoal <= arrivalRadius)
        {
            logger.Debug("Too close to destination.");
            return [];
        }

        GraphTraverser traverser = new(graph, pathfinder, logger);
        // Teleport-first: from camp this is usually instant (no vnav). DirectWalk only for short hops.
        traverser.AddCalculator(new WalkTeleportWalkCalculator());
        traverser.AddCalculator(new DirectWalkCalculator());
        traverser.AddCalculator(new ReturnWalkCalculator());

        // Don't offer Return when already closer to the goal than to camp — that caused #84 loops.
        float distToCamp = graph.GetBaseCampAetheryteNode() is { } camp
            ? player.Position.Distance2D(camp.Position)
            : float.MaxValue;
        if (distanceToGoal > NavigationConstants.MaxDirectWalkDistance
            && distanceToGoal >= distToCamp * 0.5f)
        {
            traverser.AddCalculator(new ReturnTeleportWalkCalculator());
        }

        List<PathStep> steps = await traverser.FindPath(player.Position, pathGoal);
        List<PathStep> resolvedSteps = steps
            .Select(step => AethernetNavigation.ResolveAetherytePathStep(step, zone))
            .ToList();

        if (config.StopAfterActivityAetheryte)
        {
            // Keep Return / Teleport; drop the walk to the FATE or CE (#109).
            resolvedSteps = resolvedSteps
                .Where(step => step.Kind != PathStepKind.Pathfind)
                .ToList();
            logger.Debug("StopAfterActivityAetheryte: {Count} step(s) after dropping pathfinds", resolvedSteps.Count);
        }

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
