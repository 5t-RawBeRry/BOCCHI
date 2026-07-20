using BOCCHI.Common.Data.Fates;
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
    IFateFactory factory
) : IFateRepository, IOnUpdate
{
    public event Action<Fate>? FateAdded;

    public event Action<FateId>? FateRemoved;

    public IReadOnlyList<Fate> Snapshot() => data.GetAll().ToList();

    public bool HasFate(FateId id) => data.ContainsKey(id);

    public void Update()
    {
        Dictionary<FateId, Fate> current = fates
            .Where(f => f.State is FateState.Preparing or FateState.Running)
            .Where(f => f.Position != Vector3.Zero && f.Position != Vector3.NaN)
            .Select(factory.Create)
            .ToDictionary(f => f.Id, f => f);

        RepositorySync.ApplySnapshot(data, current, FateAdded, FateRemoved);

        foreach(Fate fate in data.GetAll())
        {
            IFate? context = fates.FirstOrDefault(f => f.FateId == fate.Id.Value);
            if (context == null)
            {
                continue;
            }

            fate.Update(context);
        }
    }
}
