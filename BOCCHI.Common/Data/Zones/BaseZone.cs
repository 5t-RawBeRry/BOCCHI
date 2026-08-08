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
        // used by some CE event objects, so require proximity to an aetheryte / shard
        // (or the authored camp buff point) rather than the main camp only.
        Vector3 playerPos = player.Position;
        const float playerRange = 60f;
        const float aetheryteRange = 100f;
        float playerRangeSq = playerRange * playerRange;
        float aetheryteRangeSq = aetheryteRange * aetheryteRange;

        List<Vector3> anchors = [];
        foreach (AethernetData aetheryte in GetAetherytes())
        {
            anchors.Add(aetheryte.Position);
        }

        foreach (AethernetData shard in GetAethernetShards())
        {
            if (anchors.All(a => Vector3.DistanceSquared(a, shard.Position) > 1f))
            {
                anchors.Add(shard.Position);
            }
        }

        if (anchors.Count == 0)
        {
            anchors.Add(GetAetherytePosition());
        }

        if (GetBuffZone() is { } buffZone)
        {
            anchors.Add(buffZone.Center);
        }

        List<KnowledgeCrystalData> crystals = objects
            .Where(o => o is { ObjectKind: ObjectKind.EventObj, BaseId: KnowledgeCrystalData.BaseId })
            .Where(o => Vector3.DistanceSquared(o.Position, playerPos) <= playerRangeSq)
            .Where(o => anchors.Any(a => Vector3.DistanceSquared(o.Position, a) <= aetheryteRangeSq))
            .OrderBy(o => Vector3.DistanceSquared(o.Position, playerPos))
            .Select(o => new KnowledgeCrystalData
            {
                Position = o.Position
            })
            .ToList();

        // Authored camp buff point: still count as a crystal when the live object is
        // missing / id-mismatched but the player is standing at the known buff site.
        if (GetBuffZone() is { } zone
            && Vector3.DistanceSquared(playerPos, zone.Center) <= playerRangeSq
            && crystals.All(c => Vector3.DistanceSquared(c.Position, zone.Center) > 25f))
        {
            crystals.Add(new KnowledgeCrystalData
            {
                Position = zone.Center
            });
            crystals = crystals
                .OrderBy(c => Vector3.DistanceSquared(c.Position, playerPos))
                .ToList();
        }

        return crystals;
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
        // v7: discard suspect early caches; load validates usability and rebuilds if broken.
        const int graphSchemaVersion = 7;
        string path = Path.Combine(dir, $"{TerritoryType}.v{graphSchemaVersion}.json");

        if (File.Exists(path))
        {
            try
            {
                string json = await File.ReadAllTextAsync(path);
                ZoneGraph? loaded = ZoneGraph.FromJson(json);
                if (loaded is { } graph && graph.IsUsableForRouting())
                {
                    logger.Debug("Loaded zone graph from path: " + path);
                    cachedGraph = graph;
                    return graph;
                }

                logger.Warning(
                    "Zone graph cache is empty or missing routing edges — rebuilding ({Path})",
                    path);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to load zone graph cache — rebuilding ({Path})", path);
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                // Rebuild overwrites; delete is best-effort.
            }
        }

        logger.Info($"Building zone graph for territory {TerritoryType} (one-time; Automator waits until done)");
        GraphConfig config = new(pathfinder, logger);
        ZoneGraph built = await graphs.BuildAsync(config, this);
        logger.Debug("Writing zone graph to: " + path);
        await File.WriteAllTextAsync(path, built.ToJson());

        cachedGraph = built;
        return built;
    }

    private unsafe uint GetCurrentSubAreaPlaceNameId()
    {
        TerritoryInfo* info = TerritoryInfo.Instance();
        return info == null ? 0 : info->SubAreaPlaceNameId;
    }

    protected abstract ushort GetForkedTowerEventId();
}
