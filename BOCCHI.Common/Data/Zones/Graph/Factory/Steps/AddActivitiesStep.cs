namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public class AddActivitiesStep : IGraphBuildStep
{
    public async Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        foreach(ActivityData fate in zone.GetNormalFateData())
        {
            graph.AddNode(new()
            {
                Type = NodeType.NormalFate,
                Position = fate.Position,
                Metadata = new ActivityNodeMetadata
                {
                    Id = fate.Id,
                    PreferredAethernetId = fate.PreferredAethernetId
                }
            });
        }

        foreach(ActivityData fate in zone.GetPotFateData())
        {
            graph.AddNode(new()
            {
                Type = NodeType.PotFate,
                Position = fate.Position,
                Metadata = new ActivityNodeMetadata
                {
                    Id = fate.Id,
                    PreferredAethernetId = fate.PreferredAethernetId
                }
            });
        }

        foreach(ActivityData criticalEncounter in zone.GetCriticalEncounterData())
        {
            graph.AddNode(new()
            {
                Type = NodeType.CriticalEncounter,
                Position = criticalEncounter.Position,
                Metadata = new ActivityNodeMetadata
                {
                    Id = criticalEncounter.Id,
                    PreferredAethernetId = criticalEncounter.PreferredAethernetId,
                    AreaShape = criticalEncounter.AreaShape,
                }
            });
        }

        List<Node> activities = graph.GetActivityNodes().ToList();

        await graph.ConnectToNearestTeleports(activities, config);
        await graph.ConnectToBaseCamp(activities, config);
    }
}
