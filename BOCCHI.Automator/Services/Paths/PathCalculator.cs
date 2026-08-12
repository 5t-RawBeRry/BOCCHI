using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Config;
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

        // Prefer live FATE center when available; CEs keep authored graph staging.
        Node pathGoal = goalNode;
        Vector3? potPrepositionStandOff = null;
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
        }
        else if (goal.GoalType is FateGoal potPreposition
                 && zone.IsPotFate(potPreposition.id.Value))
        {
            potPrepositionStandOff = NavigationApproach.GetPotPrepositionPosition(goalNode.Position, player.Position);
        }

        float ceCombatRadius = 0f;
        ActivityAreaShape ceShape = ActivityAreaShape.Circle;
        if (goal.GoalType is CriticalEncounterGoal ceGoalForRadius)
        {
            ActivityData? authored = zone.GetCriticalEncounterData()
                .FirstOrDefault(a => a.Id == ceGoalForRadius.id.Value);
            ceCombatRadius = authored?.CombatRadius ?? 0f;
            ceShape = authored?.AreaShape ?? ActivityAreaShape.Circle;
            logger.Debug(
                "CE {Id} path goal at {Pos:F0} ({Shape}, combat radius {Radius:F0})",
                ceGoalForRadius.id.Value,
                pathGoal.Position,
                ceShape,
                ceCombatRadius);
        }

        Vector3 arrivalCheck = potPrepositionStandOff ?? pathGoal.Position;
        float distanceToGoal = player.Position.Distance2D(arrivalCheck);
        bool insideCeWait = ceCombatRadius > 0f
                            && NavigationConstants.IsInsideCriticalEncounterWaitArea(
                                arrivalCheck,
                                ceCombatRadius,
                                ceShape,
                                player.Position);

        if (insideCeWait)
        {
            logger.Debug("Inside CE wait area at {Pos:F0} — no travel steps", arrivalCheck);
            return [];
        }

        if (ceCombatRadius <= 0f && distanceToGoal <= NavigationConstants.EventArrivalRadius)
        {
            logger.Debug("Too close to destination ({Dist:F1}y).", distanceToGoal);
            return [];
        }

        GraphTraverser traverser = new(graph, pathfinder, logger);
        // Teleport-first: from camp this is usually instant. DirectWalk only for short hops.
        traverser.AddCalculator(new WalkTeleportWalkCalculator());
        traverser.AddCalculator(new DirectWalkCalculator());

        // Always consider Return on long trips. The old "closer than half camp" gate skipped
        // Return when already near the inbound shard, so same-shard WalkTeleportWalk walked
        // 500y+ to CEs like Lost on the Wind instead of Return → TP (#172).
        if (!insideCeWait && distanceToGoal > NavigationConstants.MaxDirectWalkDistance)
        {
            traverser.AddCalculator(new ReturnTeleportWalkCalculator());
        }

        List<PathStep> steps = await traverser.FindPath(player.Position, pathGoal);
        List<PathStep> resolvedSteps = steps
            .Select(step => AethernetNavigation.ResolveAetherytePathStep(step, zone, player.Position))
            .ToList();

        if (potPrepositionStandOff is { } standOff)
        {
            // Only rewrite the *arrival* Pathfind. Rewriting walk-to-pad Pathfinds before
            // Teleport turned Return→TP into a cross-map mount (#174).
            int lastPathfind = resolvedSteps.FindLastIndex(step => step.PathStepData is Pathfind);
            if (lastPathfind >= 0)
            {
                float range = resolvedSteps[lastPathfind].PathStepData is Pathfind(_, var r) ? r : 0f;
                resolvedSteps[lastPathfind] = PathStep.Pathfind(standOff, range);
            }
        }

        if (config.StopAfterReturn)
        {
            // Keep Return / Teleport; drop the walk to the FATE or CE.
            resolvedSteps = resolvedSteps
                .Where(step => step.Kind != PathStepKind.Pathfind)
                .ToList();
            logger.Debug("TeleportOnlyTravel: {Count} step(s) after dropping pathfinds", resolvedSteps.Count);
        }

        logger.Info(
            "Path planned: {Count} step(s) toward {Pos:F0} ({Dist:F0}y)",
            resolvedSteps.Count,
            arrivalCheck,
            distanceToGoal);

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
