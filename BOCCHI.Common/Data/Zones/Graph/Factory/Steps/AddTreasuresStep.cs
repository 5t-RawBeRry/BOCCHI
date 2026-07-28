using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Lumina.Excel.Sheets;
using Ocelot.Services.Data;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BOCCHI.Common.Data.Zones.Graph.Factory.Steps;

public class AddTreasuresStep(IDataRepository<Treasure> treasureSheet) : IGraphBuildStep
{
    public async Task ExecuteAsync(ZoneGraph graph, GraphConfig config, IZone zone)
    {
        unsafe
        {
            LayoutManager* layout = LayoutWorld.Instance()->ActiveLayout;
            if (layout == null)
            {
                return;
            }

            if (!layout->InstancesByType.TryGetValue(InstanceType.Treasure, out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> mapPtr, false))
            {
                return;
            }

            foreach(ILayoutInstance* instance in mapPtr.Value->Values)
            {
                Transform* transform = instance->GetTransformImpl();
                Vector3 position = transform->Translation;
                if (position.Y <= -10f)
                {
                    continue;
                }

                uint treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
                uint sgbId = treasureSheet.Get(treasureRowId).SGB.RowId;
                if (sgbId != 1596 && sgbId != 1597)
                {
                    continue;
                }

                int level = 99;
                foreach(TreasureData datum in zone.GetTreasureData())
                {
                    if (datum.Id == treasureRowId)
                    {
                        level = datum.Level;
                        break;
                    }
                }

                graph.AddNode(new()
                {
                    Type = NodeType.Treasure,
                    Position = position,
                    Metadata = new TreasureNodeMetadata
                    {
                        Type = sgbId == 1596 ? TreasureType.Bronze : TreasureType.Silver,
                        Level = level
                    }
                });
            }
        }

        List<Node> treasures = graph.GetNodesByTypes(NodeType.Treasure).ToList();

        await graph.ConnectToNearestTeleports(treasures, config);
        await graph.ConnectToNearestAlike(treasures, config, 4);
        await graph.ConnectToBaseCamp(treasures, config);
    }
}
