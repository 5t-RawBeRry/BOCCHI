using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using System.Numerics;

namespace BOCCHI.CriticalEncounters.Data;

public interface ICriticalEncounterFactory
{
    CriticalEncounter Create(DynamicEvent ev);
}

public class CriticalEncounterFactory(IZoneProvider zones, CriticalEncounterGeometry geometry) : ICriticalEncounterFactory
{
    public CriticalEncounter Create(DynamicEvent ev)
    {
        CriticalEncounterId id = new(ev.DynamicEventId);
        IZone zone = zones.GetZone();
        ActivityData? authored = zone.GetCriticalEncounterData()
            .FirstOrDefault(a => a.Id == ev.DynamicEventId);
        Vector3 fallback = authored?.Position ?? Vector3.NaN;

        ActivityAreaShape shape = authored?.AreaShape ?? ActivityAreaShape.Circle;
        float padded = 0f;
        if (geometry.TryGetCombat(ev.DynamicEventId, out float combat, out ActivityAreaShape lgbShape))
        {
            shape = lgbShape;
            padded = NavigationConstants.CriticalEncounterPaddedRadius(combat, shape);
            zone.ApplyCriticalEncounterCombat(ev.DynamicEventId, combat, shape);
        }

        return new(id, ev, padded, fallback, shape);
    }
}
