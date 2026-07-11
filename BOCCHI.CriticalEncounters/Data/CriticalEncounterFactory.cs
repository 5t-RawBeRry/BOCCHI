using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace BOCCHI.CriticalEncounters.Data;

public class CriticalEncounterFactory(IZoneProvider zones) : ICriticalEncounterFactory
{
    public CriticalEncounter Create(DynamicEvent ev)
    {
        var id = new CriticalEncounterId(ev.DynamicEventId);
        var radius = zones.GetZone().GetCriticalEncounterRadius(ev.DynamicEventId);

        return new CriticalEncounter(id, ev, radius);
    }
}
