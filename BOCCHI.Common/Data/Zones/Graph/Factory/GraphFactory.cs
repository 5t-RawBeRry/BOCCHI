using BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

namespace BOCCHI.Common.Data.Zones.Graph.Factory;

public class GraphFactory : IGraphFactory
{
    private readonly List<IGraphBuildStep> steps =
    [
        new AddTeleportsStep(),
        new AddActivitiesStep(),
    ];

    public async Task<ZoneGraph> BuildAsync(GraphConfig config, IZone zone)
    {
#if DEBUG
        // Replace previous rebuild's samples — avoid unbounded accumulation across cold builds.
        GraphConfig.DebugPathLines.Clear();
#endif
        ZoneGraph graph = new();

        foreach (IGraphBuildStep step in steps)
        {
            await step.ExecuteAsync(graph, config, zone);
        }

        return graph;
    }
}
