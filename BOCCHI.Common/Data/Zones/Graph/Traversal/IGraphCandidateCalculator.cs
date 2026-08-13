using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph.Traversal;

public interface IGraphCandidateCalculator
{
    string Key();

    /// <summary>
    ///     Optional <em>admissible</em> lower bound on this candidate's cost — it must never exceed
    ///     what <see cref="CalculateAsync"/> would return, and must be cheap (no vnav queries).
    ///     When the bound already matches or beats the best candidate found so far, the traverser
    ///     skips this calculator entirely, because it provably cannot win.
    ///     Return null (the default) to always evaluate.
    /// </summary>
    float? LowerBoundCost(ZoneGraph graph, Vector3 start, Node goal) => null;

    Task<TraversalCandidate?> CalculateAsync(ZoneGraph graph, Vector3 start, Node goal, IPathfinder pathfinder);
}
