namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public class AddPotChestsStep : IGraphBuildStep
{
    public async Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        await AddNormalPotChests(graph, config, zone);
        await AddRerollPotChests(graph, config, zone);
    }

    private async Task AddNormalPotChests(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        List<int> fates = new();
        foreach((int fateId, List<PotChestData> chestData) in zone.GetPotChestData())
        {
            fates.Add(fateId);

            foreach(PotChestData chest in chestData)
            {
                graph.AddNode(new()
                {
                    Type = NodeType.PotChest,
                    Position = chest.Position,
                    Metadata = new PotChestNodeMetadata
                    {
                        FateId = fateId,
                        Level = chest.Level
                    }
                });
            }
        }

        List<Node> chests = graph.GetNodesByTypes(NodeType.PotChest).ToList();
        foreach(int fate in fates)
        {
            List<Node> relevant = chests.Where(chest =>
            {
                if (chest.Metadata is not PotChestNodeMetadata meta)
                {
                    return false;
                }

                return meta.FateId == fate;
            }).ToList();

            await graph.ConnectToNearestTeleports(relevant, config);
            await graph.ConnectToNearestAlike(relevant, config, 4);
            await graph.ConnectToBaseCamp(relevant, config);
        }
    }

    private async Task AddRerollPotChests(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        foreach(PotChestData chest in zone.GetRerollPotChestData())
        {
            graph.AddNode(new()
            {
                Type = NodeType.PostChestReroll,
                Position = chest.Position,
                Metadata = new RerollPotChestNodeMetadata
                {
                    Level = chest.Level
                }
            });
        }

        List<Node> nodes = graph.GetNodesByTypes(NodeType.PostChestReroll).ToList();

        await graph.ConnectToNearestTeleports(nodes, config);
        await graph.ConnectToNearestAlike(nodes, config, 4);
        await graph.ConnectToBaseCamp(nodes, config);
    }
}
