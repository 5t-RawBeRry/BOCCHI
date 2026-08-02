using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Treasure.Hunt;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Lumina.Excel.Sheets;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.UI;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TreasureSheet = Lumina.Excel.Sheets.Treasure;

namespace BOCCHI.Debug.Panels;

public sealed class TreasureHuntPrecomputePanel
(
    IZoneProvider zones,
    IDataManager data,
    IDalamudPluginInterface plugin,
    IVNavmeshIpc vnav,
    IPluginLog log,
    IBrandingService branding,
    IUIService ui
) : IDebugPanel
{
    private readonly List<TreasureSpot> treasures = [];

    private readonly Stopwatch stopwatch = new();

    private CancellationTokenSource? runCts;

    private bool running;

    private bool finished;

    private string? lastError;

    private string? lastOutputPath;

    private uint progress;

    private uint maxProgress;

    public string Name => "Treasure Hunt Bake";

    public void Render()
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            ui.Text("Enter South Horn or North Horn first.");
            return;
        }

        if (!vnav.IsNavmeshReady())
        {
            ui.Text("vnavmesh not ready.", branding.DalamudRed);
        }

        if (ImGui.Button("Refresh layout treasures"))
        {
            RefreshTreasures(zone);
        }

        ImGui.SameLine();
        ui.LabelledValue("Spots", treasures.Count);
        ui.LabelledValue("Bronze", treasures.Count(t => t.SgbId == 1596));
        ui.LabelledValue("Silver", treasures.Count(t => t.SgbId == 1597));
        ui.LabelledValue("Aethernets", zone.GetAetherytes().Count);

        if (lastError != null)
        {
            ui.Text(lastError, branding.DalamudRed);
        }

        if (running)
        {
            float pct = maxProgress == 0 ? 0f : progress / (float)maxProgress * 100f;
            ui.LabelledValue("Progress", $"{pct:0.00}%  ({progress}/{maxProgress})");
            ui.LabelledValue("Elapsed", stopwatch.Elapsed.ToString(@"mm\:ss"));
            if (ImGui.Button("Cancel"))
            {
                runCts?.Cancel();
            }

            return;
        }

        if (finished && lastOutputPath != null)
        {
            ui.Text($"Wrote {lastOutputPath}", branding.DalamudYellow);
            ui.LabelledValue("Elapsed", stopwatch.Elapsed.ToString(@"mm\:ss"));
        }

        ui.Text("Bakes node↔node and node↔aethernet walk distances via vnav (same as South Horn).", branding.DalamudGrey);
        ui.Text("Stay in the zone with navmesh loaded. Can take a long time (~5k pathfinds).", branding.DalamudGrey);

        if (ImGui.Button("Run bake"))
        {
            RefreshTreasures(zone);
            if (treasures.Count == 0)
            {
                lastError = "No layout treasures found. Are you in the zone?";
                return;
            }

            _ = RunBakeAsync(zone);
        }
    }

    private async Task RunBakeAsync(IZone zone)
    {
        running = true;
        finished = false;
        lastError = null;
        lastOutputPath = null;
        progress = 0;
        runCts = new CancellationTokenSource();
        CancellationToken ct = runCts.Token;
        stopwatch.Restart();

        try
        {
            List<AethernetData> aethernets = zone.GetAetherytes();
            List<(HuntAethernet Hunt, Vector3 Destination)> shards = [];
            foreach(AethernetData aetheryte in aethernets)
            {
                if (!Enum.IsDefined(typeof(HuntAethernet), aetheryte.Id))
                {
                    continue;
                }

                shards.Add(((HuntAethernet)aetheryte.Id, aetheryte.GetInteractPosition()));
            }

            if (shards.Count == 0)
            {
                throw new InvalidOperationException("No HuntAethernet mappings for this zone's PlaceName IDs.");
            }

            int t = treasures.Count;
            int a = shards.Count;
            maxProgress = (uint)(t * (t - 1 + 2 * a));

            HuntNodeDataSchema schema = new();
            foreach((HuntAethernet hunt, Vector3 _) in shards)
            {
                schema.AethernetToNodeDistances[hunt] = [];
            }

            foreach(TreasureSpot treasure in treasures)
            {
                ct.ThrowIfCancellationRequested();
                schema.NodeToNodeDistances[treasure.Id] = [];
                schema.NodeToAethernetDistances[treasure.Id] = [];

                foreach(TreasureSpot other in treasures)
                {
                    if (other.Id == treasure.Id)
                    {
                        continue;
                    }

                    ct.ThrowIfCancellationRequested();
                    List<Vector3> path = await vnav.Pathfind(treasure.Position, other.Position, false, ct);
                    schema.NodeToNodeDistances[treasure.Id].Add(new(other.Id, PathLength(path), ToHuntPath(path)));
                    progress++;
                }

                foreach((HuntAethernet hunt, Vector3 destination) in shards)
                {
                    ct.ThrowIfCancellationRequested();

                    List<Vector3> fromShard = await vnav.Pathfind(destination, treasure.Position, false, ct);
                    schema.AethernetToNodeDistances[hunt].Add(new(treasure.Id, PathLength(fromShard), ToHuntPath(fromShard)));
                    progress++;

                    List<Vector3> toShard = await vnav.Pathfind(treasure.Position, destination, false, ct);
                    schema.NodeToAethernetDistances[treasure.Id].Add(new(hunt, PathLength(toShard), ToHuntPath(toShard)));
                    progress++;
                }
            }

            string json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = false });
            List<string> written = WriteOutputs(zone.ZoneId, json);
            lastOutputPath = string.Join(" | ", written);
            finished = true;
            log.Information("[TreasureHuntBake] Wrote {Paths}", lastOutputPath);
        }
        catch (OperationCanceledException)
        {
            lastError = "Cancelled.";
            log.Warning("[TreasureHuntBake] Cancelled");
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            log.Error(ex, "[TreasureHuntBake] Failed");
        }
        finally
        {
            stopwatch.Stop();
            running = false;
            runCts?.Dispose();
            runCts = null;
        }
    }

    private unsafe void RefreshTreasures(IZone zone)
    {
        treasures.Clear();
        LayoutManager* layout = LayoutWorld.Instance()->ActiveLayout;
        if (layout == null)
        {
            return;
        }

        if (!layout->InstancesByType.TryGetValue(InstanceType.Treasure, out Pointer<StdMap<ulong, Pointer<ILayoutInstance>>> mapPtr, false))
        {
            return;
        }

        List<TreasureData> authored = zone.GetTreasureData();
        bool hasPositionData = authored.Exists(d => d.Position.HasValue);

        foreach(ILayoutInstance* instance in mapPtr.Value->Values)
        {
            Transform* transform = instance->GetTransformImpl();
            Vector3 position = transform->Translation;
            if (position.Y <= -10f && !hasPositionData)
            {
                continue;
            }

            uint treasureRowId = Unsafe.Read<uint>((byte*)instance + 0x30);
            uint sgbId = data.GetExcelSheet<TreasureSheet>().GetRow(treasureRowId).SGB.RowId;
            if (sgbId != 1596 && sgbId != 1597)
            {
                continue;
            }

            if (hasPositionData && !authored.Any(d => d.Matches(treasureRowId, position)))
            {
                continue;
            }

            treasures.Add(new(treasureRowId, position, sgbId));
        }

        treasures.Sort((a, b) => a.Id.CompareTo(b.Id));
    }

    private List<string> WriteOutputs(ZoneId zoneId, string json)
    {
        string zoneFolder = zoneId switch
        {
            ZoneId.SouthHorn => "SouthHorn",
            ZoneId.NorthHorn => "NorthHorn",
            var _ => throw new NotSupportedException($"No hunt data folder for {zoneId}")
        };

        const string filename = "precomputed_treasure_hunt_data.json";
        List<string> written = [];

        string? pluginDir = plugin.AssemblyLocation.DirectoryName;
        if (!string.IsNullOrEmpty(pluginDir))
        {
            string runtimePath = Path.Combine(pluginDir, "Data", zoneFolder, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
            File.WriteAllText(runtimePath, json);
            written.Add(runtimePath);
        }

        string? sourceRoot = FindRepoTreasureDataRoot(pluginDir);
        if (sourceRoot != null)
        {
            string sourcePath = Path.Combine(sourceRoot, zoneFolder, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, json);
            written.Add(sourcePath);
        }

        return written;
    }

    private static string? FindRepoTreasureDataRoot(string? start)
    {
        string? dir = start;
        for(int i = 0; i < 8 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "BOCCHI.Treasure", "Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static float PathLength(List<Vector3> path)
    {
        float length = 0f;
        for(int i = 1; i < path.Count; i++)
        {
            length += Vector3.Distance(path[i - 1], path[i]);
        }

        return length;
    }

    private static List<HuntPosition> ToHuntPath(List<Vector3> path) =>
        path.Select(p => new HuntPosition { X = p.X, Y = p.Y, Z = p.Z }).ToList();

    private readonly record struct TreasureSpot(uint Id, Vector3 Position, uint SgbId);
}
