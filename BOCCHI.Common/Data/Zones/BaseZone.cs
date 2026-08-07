using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Zones.Graph.Factory;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Ocelot.Extensions;
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

    /// <summary>
    ///     True at expedition base camp. SubAreaPlaceNameId is unreliable (duplicate PlaceName
    ///     rows / lag), so also accept proximity to the main aetheryte — otherwise Return loops
    ///     forever after Demi-Return lands "in town".
    /// </summary>
    public bool IsInBasecamp()
    {
        if (GetCurrentSubAreaPlaceNameId() == BasecampPlaceNameId)
        {
            return true;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return false;
        }

        const float campRadius = 80f;
        return player.Position.Distance2D(GetAetherytePosition()) <= campRadius;
    }

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

    // Authored carrot points for a future full-zone tour (nearby MVP uses live objects only).
    public virtual List<CarrotData> GetCarrotData() => [];

    public virtual BuffZone? GetBuffZone() => null;

    public virtual TreasureRoutePolicy GetTreasureRoutePolicy() => new();

    public virtual ShoppingVendorData? GetShoppingVendor() => null;

    public List<KnowledgeCrystalData> GetNearbyKnowledgeCrystals()
    {
        if (objects.LocalPlayer is not { } player)
        {
            return [];
        }

        // Do not gate on IsInBasecamp() — SubAreaPlaceNameId often does not match the
        // authored BasecampPlaceNameId even while standing at camp. Same BaseId is also
        // used by some CE event objects, so require proximity to the main aetheryte.
        Vector3 playerPos = player.Position;
        Vector3 camp = GetAetherytePosition();
        const float playerRange = 60f;
        const float campRange = 100f;

        return objects
            .Where(o => o is { ObjectKind: ObjectKind.EventObj, BaseId: KnowledgeCrystalData.BaseId })
            .Where(o => Vector3.Distance(o.Position, camp) <= campRange)
            .Where(o => Vector3.Distance(o.Position, playerPos) <= playerRange)
            .OrderBy(o => Vector3.DistanceSquared(o.Position, playerPos))
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
        // v6: Eye to Eye prefers Crown of Karnak (Unhallowed is Euclidean-near but cut off).
        const int graphSchemaVersion = 6;
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
