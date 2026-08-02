namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public interface IGraphBuildStep
{
    Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone);
}
