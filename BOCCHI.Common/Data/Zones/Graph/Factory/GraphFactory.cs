using BOCCHI.Common.Data.Zones.Graph.Factory.Steps;
using Lumina.Excel.Sheets;
using Ocelot.Services.Data;

namespace BOCCHI.Common.Data.Zones.Graph.Factory;

public class GraphFactory : IGraphFactory
{
    private readonly List<IGraphBuildStep> steps = [];

    public GraphFactory(IDataRepository<Treasure> treasureSheet)
    {
        steps.Add(new AddTeleportsStep());
        steps.Add(new AddActivitiesStep());
        // Treasures / pots / carrots are not used by Automator pathing (treasure hunt and
        // pot farm use their own data). Wiring them with vnav pathfinds floods the query
        // queue for minutes on cold graph builds.
        _ = treasureSheet;
    }

    public async Task<ZoneGraph> BuildAsync(GraphConfig config, IZone zone)
    {
#if DEBUG
        // Replace previous rebuild's samples — avoid unbounded accumulation across cold builds.
        GraphConfig.DebugPathLines.Clear();
#endif
        ZoneGraph graph = new();

        foreach(IGraphBuildStep step in steps)
        {
            await step.ExecuteAsync(graph, config, zone);
        }

        return graph;
    }
}
