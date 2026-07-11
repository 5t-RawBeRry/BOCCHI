using BOCCHI.Common.Data.CriticalEncounters;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace BOCCHI.CriticalEncounters.Data;

public class CriticalEncounterFactory : ICriticalEncounterFactory
{
    public CriticalEncounter Create(DynamicEvent ev)
    {
        var id = new CriticalEncounterId(ev.DynamicEventId);

        return new CriticalEncounter(id, ev);
    }
}
