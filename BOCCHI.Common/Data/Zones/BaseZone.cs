using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Zones.Graph.Factory;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;
using Path = System.IO.Path;

namespace BOCCHI.Common.Data.Zones;

public abstract class BaseZone
(
    IObjectTable objects,
    IDalamudPluginInterface plugin,
    IGraphFactory graphs,
    IPathfinder pathfinder,
    ILogger logger,
    ZoneId zoneId
) : IZone
{
    protected abstract uint BasecampPlaceNameId { get; }

    public ZoneId ZoneId
    {
        get => zoneId;
    }

    public ushort TerritoryType => (ushort)ZoneId;

    public ushort ForkedTowerEventId => GetForkedTowerEventId();

    public bool IsOccultCrescentZone() => true;

    public bool IsInBasecamp() => GetCurrentSubAreaPlaceNameId() == BasecampPlaceNameId;

    public abstract AethernetData GetMainAetheryte();

    public abstract Vector3 GetAetherytePosition();

    public abstract Vector3 GetStartingPosition();

    public virtual List<AethernetData> GetAetherytes() => [];

    public virtual List<AethernetData> GetAethernetShards() => [];

    public virtual List<ActivityData> GetNormalFateData() => [];

    public virtual List<ActivityData> GetPotFateData() => [];

    public virtual List<ActivityData> GetCriticalEncounterData() => [];

    public virtual List<TreasureData> GetTreasureData() => [];

    public virtual Dictionary<int, List<PotChestData>> GetPotChestData() => [];

    public virtual List<PotChestData> GetRerollPotChestData() => [];

    public virtual List<CarrotData> GetCarrotData() => [];

    public List<KnowledgeCrystalData> GetNearbyKnowledgeCrystals()
    {
        // TODO: Fix Knowledge Crystal identification.
        // The base id we use below isn't unique to knowledge crystals and seems to refer to any event object in OC, this includes the CE zone and CE spawn in zone
        if (!IsInBasecamp())
        {
            return [];
        }

        return objects
            .Where(o => o is { ObjectKind: ObjectKind.EventObj, BaseId: KnowledgeCrystalData.BaseId })
            .Select(o => new KnowledgeCrystalData
            {
                Position = o.Position
            })
            .ToList();
    }

    public virtual float GetCriticalEncounterRadius(int eventId)
    {
        ActivityData? activity = GetCriticalEncounterData().FirstOrDefault(a => a.Id == eventId);
        return activity?.CombatRadius is { } radius
            ? radius + NavigationConstants.CriticalEncounterRadiusPadding
            : 0f;
    }

    public unsafe bool IsInForkedTower()
    {
        DynamicEventContainer* dec = DynamicEventContainer.GetInstance();

        return dec != null && dec->CurrentEventId == GetForkedTowerEventId();
    }

    private ZoneGraph? cachedGraph;
    private Task<ZoneGraph>? graphLoadTask;
    private readonly object graphGate = new();

    public Task<ZoneGraph> GetGraph()
    {
        if (cachedGraph != null)
        {
            return Task.FromResult(cachedGraph);
        }

        lock (graphGate)
        {
            if (cachedGraph != null)
            {
                return Task.FromResult(cachedGraph);
            }

            return graphLoadTask ??= LoadOrBuildGraphAsync();
        }
    }

    private async Task<ZoneGraph> LoadOrBuildGraphAsync()
    {
        string dir = Path.Combine(plugin.GetPluginConfigDirectory(), "zone_graphs");
        Directory.CreateDirectory(dir);

        // Bump when walk-cost / edge semantics or which nodes are wired change.
        const int graphSchemaVersion = 4;
        string path = Path.Combine(dir, $"{TerritoryType}.v{graphSchemaVersion}.json");

        if (File.Exists(path))
        {
            logger.Debug("Loaded zone graph from path: " + path);
            string json = await File.ReadAllTextAsync(path);
            ZoneGraph loaded = ZoneGraph.FromJson(json);
            cachedGraph = loaded;
            return loaded;
        }

        logger.Info($"Building zone graph for territory {TerritoryType} (one-time; Automator waits until done)");
        logger.Debug("Data: " + GetNormalFateData().Count);
        GraphConfig config = new(pathfinder, logger);
        ZoneGraph graph = await graphs.BuildAsync(config, this);
        logger.Debug("Writing zone graph to: " + path);
        await File.WriteAllTextAsync(path, graph.ToJson());

        cachedGraph = graph;
        return graph;
    }

    private unsafe uint GetCurrentSubAreaPlaceNameId()
    {
        TerritoryInfo* info = TerritoryInfo.Instance();
        return info == null ? 0 : info->SubAreaPlaceNameId;
    }

    protected abstract ushort GetForkedTowerEventId();
}
