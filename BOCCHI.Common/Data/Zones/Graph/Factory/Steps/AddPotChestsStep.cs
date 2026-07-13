using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Lumina.Excel.Sheets;
using Ocelot.Extensions;
using Ocelot.Services.Data;

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
        var fates = new List<int>();
        foreach (var (fateId, chestData) in zone.GetPotChestData())
        {
            fates.Add(fateId);

            foreach (var chest in chestData)
            {
                graph.AddNode(new Node
                {
                    Type = NodeType.PotChest,
                    Position = chest.Position,
                    Metadata = new PotChestNodeMetadata
                    {
                        FateId = fateId,
                        Level = chest.Level,
                    },
                });
            }
        }

        var chests = graph.GetNodesByTypes(NodeType.PotChest).ToList();
        foreach (var fate in fates)
        {
            var relevant = chests.Where(chest =>
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
        foreach (var chest in zone.GetRerollPotChestData())
        {
            graph.AddNode(new Node
            {
                Type = NodeType.PostChestReroll,
                Position = chest.Position,
                Metadata = new RerollPotChestNodeMetadata
                {
                    Level = chest.Level,
                },
            });
        }

        var nodes = graph.GetNodesByTypes(NodeType.PostChestReroll).ToList();

        await graph.ConnectToNearestTeleports(nodes, config);
        await graph.ConnectToNearestAlike(nodes, config, 4);
        await graph.ConnectToBaseCamp(nodes, config);
    }
}