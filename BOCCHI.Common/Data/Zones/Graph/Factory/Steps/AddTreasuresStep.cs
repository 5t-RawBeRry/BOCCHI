using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Lumina.Excel.Sheets;
using Ocelot.Extensions;
using Ocelot.Services.Data;

namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public class AddTreasuresStep(IDataRepository<Treasure> treasureSheet) : IGraphBuildStep
{
    public async Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        unsafe
        {
            var layout = LayoutWorld.Instance()->ActiveLayout;
            if (layout == null)
            {
                return;
            }

            if (!layout->InstancesByType.TryGetValue(InstanceType.Treasure, out var mapPtr, false))
            {
                return;
            }

            foreach (ILayoutInstance* instance in mapPtr.Value->Values)
            {
                var transform = instance->GetTransformImpl();
                var position = transform->Translation;
                if (position.Y <= -10f)
                {
                    continue;
                }

                var treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
                var sgbId = treasureSheet.Get(treasureRowId).SGB.RowId;
                if (sgbId != 1596 && sgbId != 1597)
                {
                    continue;
                }

                var level = 99;
                foreach (var datum in zone.GetTreasureData())
                {
                    if (datum.Id == treasureRowId)
                    {
                        level = datum.Level;
                        break;
                    }
                }

                graph.AddNode(new Node
                {
                    Type = NodeType.Treasure,
                    Position = position,
                    Metadata = new TreasureNodeMetadata
                    {
                        Type = sgbId == 1596 ? TreasureType.Bronze : TreasureType.Silver,
                        Level = level,
                    },
                });
            }
        }

        var treasures = graph.GetNodesByTypes(NodeType.Treasure).ToList();

        await graph.ConnectToNearestTeleports(treasures, config);
        await graph.ConnectToNearestAlike(treasures, config, 4);
        await graph.ConnectToBaseCamp(treasures, config);
    }
}