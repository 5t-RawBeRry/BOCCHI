using BOCCHI.Common.Data.Paths;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public record TraversalCandidate(float TotalCost, List<PathStep> Steps);

public class GraphTraverser(ZoneGraph graph, IPathfinder pathfinder, ILogger logger)
{
    public const float ReturnCost = 40f;

    public const float TeleportCost = 10f;

    private readonly List<IGraphCandidateCalculator> calculators = [];

    public void AddCalculator(IGraphCandidateCalculator calculator)
    {
        calculators.Add(calculator);
    }

    public async Task<List<PathStep>> FindPath(Vector3 start, Node goal)
    {
        List<TraversalCandidate> candidates = new();

        // Calculators that can bound themselves cheaply run last, so the ones already evaluated
        // set the bar. That lets an expensive candidate (a long vnav query) be skipped when its
        // lower bound proves it cannot win — instead of gating it on a fixed distance, which threw
        // away the correct answer whenever you were already partway to the goal.
        List<(IGraphCandidateCalculator Calculator, float Bound)> deferred = [];

        foreach(IGraphCandidateCalculator calculator in calculators)
        {
            if (calculator.LowerBoundCost(graph, start, goal) is { } bound)
            {
                deferred.Add((calculator, bound));
                continue;
            }

            await Evaluate(calculator, start, goal, candidates);
        }

        foreach((IGraphCandidateCalculator calculator, float bound) in deferred)
        {
            float best = candidates.Count > 0
                ? candidates.Min(c => c.TotalCost)
                : float.PositiveInfinity;

            if (bound >= best)
            {
                logger.Info(
                    $"Skipping Calculator: {calculator.Key()} (lower bound {bound:F0} >= best {best:F0})");
                continue;
            }

            await Evaluate(calculator, start, goal, candidates);
        }

        return candidates.MinBy(c => c.TotalCost)?.Steps ?? [];
    }

    private async Task Evaluate(
        IGraphCandidateCalculator calculator,
        Vector3 start,
        Node goal,
        List<TraversalCandidate> candidates)
    {
        logger.Info($"Running Calculator: {calculator.Key()}");
        TraversalCandidate? candidate = await calculator.CalculateAsync(graph, start, goal, pathfinder);
        if (candidate != null)
        {
            logger.Info($"  Cost:  {candidate.TotalCost}");
            candidates.Add(candidate);
        }
        else
        {
            logger.Info("  Cost: N/A");
        }
    }
}
