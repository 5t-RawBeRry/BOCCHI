using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Data;
using BOCCHI.CriticalEncounters.Data;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Lifecycle;
using Ocelot.Services.Data;

namespace BOCCHI.CriticalEncounters.Services;

public class CriticalEncounterRepository(
    IDataRepository<CriticalEncounterId, CriticalEncounter> data,
    ICriticalEncounterFactory factory
) : ICriticalEncounterRepository, IOnUpdate
{
    public event Action<CriticalEncounter>? CriticalEncounterAdded;

    public event Action<CriticalEncounterId>? CriticalEncounterRemoved;

    public IReadOnlyList<CriticalEncounter> Snapshot()
    {
        return data.GetAll().ToList();
    }

    public IReadOnlyList<CriticalEncounter> SnapshotWithoutForkedTower()
    {
        return data.Where(e => e.Id.Value != 48).ToList().AsReadOnly();
    }

    public bool HasCriticalEncounter(CriticalEncounterId id)
    {
        return data.ContainsKey(id);
    }

    public unsafe void Update()
    {
        var oc = PublicContentOccultCrescent.GetInstance();
        if (oc == null)
        {
            foreach (var id in data.GetKeys().ToList())
            {
                data.Remove(id);
            }

            return;
        }

        var current = oc->DynamicEventContainer.Events
            .ToArray()
            .Where(e => e.State != DynamicEventState.Inactive)
            .Select(factory.Create)
            .ToDictionary(k => k.Id, v => v);

        RepositorySync.ApplySnapshot(data, current, CriticalEncounterAdded, CriticalEncounterRemoved);

        foreach (var criticalEncounter in data.GetAll())
        {
            var ev = oc->DynamicEventContainer.Events.ToArray().FirstOrNull(e => e.DynamicEventId == criticalEncounter.Id.Value);
            if (ev == null)
            {
                continue;
            }

            criticalEncounter.Update(ev.Value);
        }
    }
}
