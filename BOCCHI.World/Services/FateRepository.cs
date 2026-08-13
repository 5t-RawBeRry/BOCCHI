using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.Services.Data;
using BOCCHI.Fates.Data;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Data;
using System.Numerics;
using FateState = Dalamud.Game.ClientState.Fates.FateState;

namespace BOCCHI.Fates.Services;

public class FateRepository
(
    IDataRepository<FateId, Fate> data,
    IFateTable fates,
    IFateFactory factory,
    IZoneProvider zones
) : IFateRepository, IOnUpdate
{
    public event Action<Fate>? FateAdded;

    public event Action<FateId>? FateRemoved;

    /// <summary>
    ///     Materialised once per Update rather than per call — this is read from a dozen places each
    ///     tick (goal validation, path calculation, pot cycle, activity choice) and every one of them
    ///     used to allocate its own copy of the list.
    /// </summary>
    private IReadOnlyList<Fate> snapshot = [];

    public IReadOnlyList<Fate> Snapshot() => snapshot;

    public bool HasFate(FateId id) => data.ContainsKey(id);

    public void Update()
    {
        // Every consumer of this repository is Occult Crescent only, but the rebuild ran everywhere
        // in the game — and the overworld is full of FATEs. Drop what we have (so subscribers see
        // the removals once) and skip the work outside OC.
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            if (snapshot.Count > 0)
            {
                RepositorySync.ApplySnapshot(data, new Dictionary<FateId, Fate>(), FateAdded, FateRemoved);
                snapshot = [];
            }

            return;
        }

        // One pass over the fate table. The refresh loop below used to re-scan it per tracked fate
        // (FirstOrDefault by id), which is O(n^2) every frame.
        Dictionary<ushort, IFate> live = [];
        Dictionary<FateId, Fate> current = [];
        foreach (IFate fate in fates)
        {
            live[fate.FateId] = fate;

            if (fate.State is not (FateState.Preparing or FateState.Running)
                || fate.Position == Vector3.Zero
                || fate.Position == Vector3.NaN)
            {
                continue;
            }

            Fate created = factory.Create(fate);
            current[created.Id] = created;
        }

        RepositorySync.ApplySnapshot(data, current, FateAdded, FateRemoved);

        List<Fate> tracked = data.GetAll().ToList();
        foreach(Fate fate in tracked)
        {
            if (live.TryGetValue(fate.Id.Value, out IFate? context))
            {
                fate.Update(context);
            }
        }

        snapshot = tracked;
    }
}
