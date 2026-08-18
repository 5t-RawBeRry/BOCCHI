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
        Vector3? lgbCenter = null;
        float combat = 0f;
        if (geometry.TryGet(ev.DynamicEventId) is { Radius: > 0 } area)
        {
            shape = area.IsSquare ? ActivityAreaShape.Square : ActivityAreaShape.Circle;
            combat = area.Radius;
            padded = NavigationConstants.CriticalEncounterPaddedRadius(combat, shape);
            lgbCenter = area.Center;
            zone.ApplyCriticalEncounterCombat(ev.DynamicEventId, combat, shape);
        }

        CriticalEncounter created = new(id, ev, padded, fallback, shape);
        if (lgbCenter is { } center)
        {
            created.ApplyCombatGeometry(combat, shape, center);
        }

        return created;
    }
}
