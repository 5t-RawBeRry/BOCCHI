using System.Numerics;
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
using Path = System.IO.Path;

namespace BOCCHI.Common.Data.Zones;

public abstract class BaseZone(
    IObjectTable objects,
    IDalamudPluginInterface plugin,
    IGraphFactory graphs,
    IPathfinder pathfinder,
    ILogger logger,
    ZoneId zoneId
) : IZone
{
    private readonly ZoneId id = zoneId;

    protected abstract uint BasecampPlaceNameId { get; }

    public ZoneId ZoneId => id;

    public ushort TerritoryType => (ushort)id;

    public ushort ForkedTowerEventId => GetForkedTowerEventId();

    public bool IsOccultCrescentZone()
    {
        return true;
    }

    private unsafe uint GetCurrentSubAreaPlaceNameId()
    {
        var info = TerritoryInfo.Instance();
        return info == null ? 0 : info->SubAreaPlaceNameId;
    }

    public bool IsInBasecamp()
    {
        return GetCurrentSubAreaPlaceNameId() == BasecampPlaceNameId;
    }

    public abstract AethernetData GetMainAetheryte();

    public abstract Vector3 GetAetherytePosition();

    public abstract Vector3 GetStartingPosition();

    public virtual List<AethernetData> GetAetherytes()
    {
        return [];
    }

    public virtual List<AethernetData> GetAethernetShards()
    {
        return [];
    }

    public virtual List<AethernetData> GetNearbyAethernetShards()
    {
        return [];
    }

    public virtual List<KnowledgeCrystalData> GetKnowledgeCrystals()
    {
        return [];
    }

    public virtual List<ActivityData> GetNormalFateData()
    {
        return [];
    }

    public virtual List<ActivityData> GetPotFateData()
    {
        return [];
    }

    public virtual List<ActivityData> GetCriticalEncounterData()
    {
        return [];
    }

    public virtual List<TreasureData> GetTreasureData()
    {
        return [];
    }

    public virtual Dictionary<int, List<PotChestData>> GetPotChestData()
    {
        return [];
    }

    public virtual List<PotChestData> GetRerollPotChestData()
    {
        return [];
    }

    public virtual List<CarrotData> GetCarrotData()
    {
        return [];
    }

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
                Position = o.Position,
            })
            .ToList();
    }

    public virtual float GetCriticalEncounterRadius(int eventId)
    {
        var activity = GetCriticalEncounterData().FirstOrDefault(a => a.Id == eventId);
        return activity?.CombatRadius is { } radius
            ? radius + NavigationConstants.CriticalEncounterRadiusPadding
            : 0f;
    }

    protected abstract ushort GetForkedTowerEventId();

    public unsafe bool IsInForkedTower()
    {
        var dec = DynamicEventContainer.GetInstance();

        return dec != null && dec->CurrentEventId == GetForkedTowerEventId();
    }

    public async Task<ZoneGraph> GetGraph()
    {
        var dir = Path.Combine(plugin.GetPluginConfigDirectory(), "zone_graphs");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{TerritoryType}.json");

        if (File.Exists(path))
        {
            logger.Debug("Loaded zone graph from path: " + path);
            var json = await File.ReadAllTextAsync(path);
            return ZoneGraph.FromJson(json);
        }

        logger.Debug("Creating new zone graph");
        logger.Debug("Data: " + GetNormalFateData().Count);
        var config = new GraphConfig(pathfinder, logger);
        var graph = await graphs.BuildAsync(config, this);
        logger.Debug("Writing zone graph to: " + path);
        await File.WriteAllTextAsync(path, graph.ToJson());

        return graph;
    }
}
