namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public class AddCarrotsStep : IGraphBuildStep
{
    public async Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        foreach(CarrotData carrot in zone.GetCarrotData())
        {
            graph.AddNode(new()
            {
                Type = NodeType.Carrot,
                Position = carrot.Position,
                Metadata = new CarrotNodeMetadata { Level = carrot.Level }
            });
        }

        List<Node> carrots = graph.GetNodesByTypes(NodeType.Carrot).ToList();

        await graph.ConnectToNearestTeleports(carrots, config);
        await graph.ConnectToNearestAlike(carrots, config);
        await graph.ConnectToBaseCamp(carrots, config);
    }
}
