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

    /// <summary>Materialised once per Update — see <see cref="FateRepository"/> for the rationale.</summary>
    private IReadOnlyList<CriticalEncounter> snapshot = [];

    /// <summary>
    ///     Also materialised per Update. This is the variant nearly everything reads (goal
    ///     validation, CE handlers from both GetScore and Handle, the CE context, renderers), and
    ///     it used to build a filtered list plus a read-only wrapper on every single call.
    /// </summary>
    private IReadOnlyList<CriticalEncounter> snapshotWithoutForkedTower = [];

    public IReadOnlyList<CriticalEncounter> Snapshot() => snapshot;

    public IReadOnlyList<CriticalEncounter> SnapshotWithoutForkedTower() => snapshotWithoutForkedTower;

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

            snapshot = [];
            snapshotWithoutForkedTower = [];
            return;
        }

        // One pass: the refresh loop below used to re-scan the event array per tracked encounter.
        DynamicEvent[] events = oc->DynamicEventContainer.Events.ToArray();
        Dictionary<uint, DynamicEvent> live = [];
        Dictionary<CriticalEncounterId, CriticalEncounter> current = [];
        foreach (DynamicEvent ev in events)
        {
            live[ev.DynamicEventId] = ev;

            if (ev.State == DynamicEventState.Inactive)
            {
                continue;
            }

            CriticalEncounter created = factory.Create(ev);
            current[created.Id] = created;
        }

        RepositorySync.ApplySnapshot(data, current, CriticalEncounterAdded, CriticalEncounterRemoved);

        List<CriticalEncounter> tracked = data.GetAll().ToList();
        foreach (CriticalEncounter criticalEncounter in tracked)
        {
            if (live.TryGetValue(criticalEncounter.Id.Value, out DynamicEvent ev))
            {
                criticalEncounter.Update(ev);
            }
        }

        snapshot = tracked;

        ushort forkedTowerId = zones.GetZone().ForkedTowerEventId;
        snapshotWithoutForkedTower = forkedTowerId == 0
            ? tracked
            : tracked.Where(e => e.Id.Value != forkedTowerId).ToList();
    }
}
