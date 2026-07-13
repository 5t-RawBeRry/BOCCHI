using BOCCHI.Common.Data.Aethernet;
using Ocelot.Extensions;

namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public class AddTeleportsStep : IGraphBuildStep
{
    public async Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        var start = new Node
        {
            Type = NodeType.BaseCampReturnPosition,
            Position = zone.GetStartingPosition(),
            Metadata = new TeleportNodeMetadata
            {
                Unlocked = true,
            }
        };

        var mainAetheryte = zone.GetMainAetheryte();
        var basecamp = new Node
        {
            Type = NodeType.BaseCampAetheryte,
            Position = zone.GetAetherytePosition(),
            Metadata = new TeleportNodeMetadata { AetheryteId = mainAetheryte.Id, Destination = mainAetheryte.GetInteractPosition()},
        };

        var aethernet = new List<Node>();
        foreach (var shard in zone.GetAethernetShards())
        {
            aethernet.Add(new Node
            {
                Type = NodeType.AethernetShard,
                Position = shard.Position,
                Metadata = new TeleportNodeMetadata { AetheryteId = shard.Id, Destination = shard.Destination},
            });
        }

        graph.AddNode(start);
        graph.AddNode(basecamp);
        foreach (var shard in aethernet)
        {
            graph.AddNode(shard);
        }


        graph.AddEdge(start.Id, basecamp.Id, await config.GetWalkingCost(start.Position,  mainAetheryte.Destination), EdgeType.Walk);

        foreach (var shard in aethernet)
        {
            graph.AddTwoWayEdge(basecamp.Id, shard.Id, config.TeleportCost, EdgeType.Teleport);

            foreach (var other in aethernet)
            {
                if (other.Id == shard.Id)
                {
                    continue;
                }

                graph.AddEdge(shard.Id, other.Id, config.TeleportCost, EdgeType.Teleport);
            }
        }
    }
}
