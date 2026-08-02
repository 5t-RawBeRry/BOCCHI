using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
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
        float radius = zone.GetCriticalEncounterRadius(ev.DynamicEventId);
        Vector3 fallback = zone.GetCriticalEncounterData()
            .FirstOrDefault(a => a.Id == ev.DynamicEventId)
            ?.Position ?? Vector3.NaN;

        return new(id, ev, radius, fallback);
    }
}
