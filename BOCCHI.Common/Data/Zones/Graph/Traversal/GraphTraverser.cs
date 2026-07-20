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

        foreach(IGraphCandidateCalculator calculator in calculators)
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

        return candidates.MinBy(c => c.TotalCost)?.Steps ?? [];
    }
}
