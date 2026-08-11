using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using System.Numerics;

namespace BOCCHI.CriticalEncounters.Data;

public interface ICriticalEncounterFactory
{
    CriticalEncounter Create(DynamicEvent ev);
}

public class CriticalEncounterFactory(IZoneProvider zones) : ICriticalEncounterFactory
{
    public CriticalEncounter Create(DynamicEvent ev)
    {
        CriticalEncounterId id = new(ev.DynamicEventId);
        IZone zone = zones.GetZone();
        ActivityData? authored = zone.GetCriticalEncounterData()
            .FirstOrDefault(a => a.Id == ev.DynamicEventId);
        float radius = zone.GetCriticalEncounterRadius(ev.DynamicEventId);
        Vector3 fallback = authored?.Position ?? Vector3.NaN;
        ActivityAreaShape shape = authored?.AreaShape ?? ActivityAreaShape.Circle;

        return new(id, ev, radius, fallback, shape);
    }
}
