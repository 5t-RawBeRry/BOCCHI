using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

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
        float radius = zones.GetZone().GetCriticalEncounterRadius(ev.DynamicEventId);

        return new(id, ev, radius);
    }
}
