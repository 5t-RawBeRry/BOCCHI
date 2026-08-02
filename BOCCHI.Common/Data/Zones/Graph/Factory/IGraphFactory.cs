namespace BOCCHI.Common.Data.Zones.Graph.Factory;

public interface IGraphFactory
{
    Task<ZoneGraph> BuildAsync(GraphConfig config, IZone zone);
}
