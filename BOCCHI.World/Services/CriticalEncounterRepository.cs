using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Data;
using BOCCHI.CriticalEncounters.Data;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Lifecycle;
using Ocelot.Services.Data;

namespace BOCCHI.CriticalEncounters.Services;

public class CriticalEncounterRepository
(
    IDataRepository<CriticalEncounterId, CriticalEncounter> data,
    ICriticalEncounterFactory factory,
    IZoneProvider zones
) : ICriticalEncounterRepository, IOnUpdate
{
    public event Action<CriticalEncounter>? CriticalEncounterAdded;

    public event Action<CriticalEncounterId>? CriticalEncounterRemoved;

    public IReadOnlyList<CriticalEncounter> Snapshot() => data.GetAll().ToList();

    public IReadOnlyList<CriticalEncounter> SnapshotWithoutForkedTower()
    {
        ushort forkedTowerId = zones.GetZone().ForkedTowerEventId;
        return data.Where(e => e.Id.Value != forkedTowerId).ToList().AsReadOnly();
    }

    public CriticalEncounter? TryGetForkedTower()
    {
        ushort forkedTowerId = zones.GetZone().ForkedTowerEventId;
        if (forkedTowerId == 0)
        {
            return null;
        }

        return data.GetAll().FirstOrDefault(e => e.Id.Value == forkedTowerId);
    }

    public bool HasCriticalEncounter(CriticalEncounterId id) => data.ContainsKey(id);

    public unsafe void Update()
    {
        PublicContentOccultCrescent* oc = PublicContentOccultCrescent.GetInstance();
        if (oc == null)
        {
            foreach(CriticalEncounterId id in data.GetKeys().ToList())
            {
                data.Remove(id);
            }

            return;
        }

        Dictionary<CriticalEncounterId, CriticalEncounter> current = oc->DynamicEventContainer.Events
            .ToArray()
            .Where(e => e.State != DynamicEventState.Inactive)
            .Select(factory.Create)
            .ToDictionary(k => k.Id, v => v);

        RepositorySync.ApplySnapshot(data, current, CriticalEncounterAdded, CriticalEncounterRemoved);

        foreach(CriticalEncounter criticalEncounter in data.GetAll())
        {
            DynamicEvent? ev = oc->DynamicEventContainer.Events.ToArray().FirstOrNull(e => e.DynamicEventId == criticalEncounter.Id.Value);
            if (ev == null)
            {
                continue;
            }

            criticalEncounter.Update(ev.Value);
        }
    }
}
