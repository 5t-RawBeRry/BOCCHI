using BOCCHI.Common.Data.SupportJobs;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Lumina.Excel.Sheets;
using Ocelot.Services.Data;
namespace BOCCHI.Data;

public class SupportJobFactory : ISupportJobFactory
{
    private readonly Lazy<SupportJob[]> all;

    private readonly IDataRepository<MKDSupportJob> jobData;

    private readonly ISubrowDataRepository<MKDGrowDataSJob> jobGrowthData;
    private readonly IDataRepository<SupportJobId, SupportJob> repo;

    public SupportJobFactory(
        IDataRepository<SupportJobId, SupportJob> repo,
        IDataRepository<MKDSupportJob> jobData,
        ISubrowDataRepository<MKDGrowDataSJob> jobGrowthData
    )
    {
        this.repo = repo;
        this.jobData = jobData;
        this.jobGrowthData = jobGrowthData;
        all = new(() => Enum.GetValues<SupportJobId>().Select(Create).ToArray());
    }


    public SupportJob Create(SupportJobId id)
    {
        if (!repo.TryGet(id, out SupportJob supportJob))
        {
            uint rowId = id.RowId();
            supportJob = new()
            {
                Id = id,
                Data = jobData.Get(rowId),
                GrowthData = jobGrowthData.GetRow(rowId)
            };

            repo.Add(id, supportJob);
        }

        return supportJob;
    }

    public unsafe bool TryGetCurrent(out SupportJob current)
    {
        current = null!;
        OccultCrescentState* state = PublicContentOccultCrescent.GetState();
        if (state == null)
        {
            return false;
        }

        int raw = state->CurrentSupportJob;

        if (!Enum.IsDefined(typeof(SupportJobId), raw))
        {
            return false;
        }

        current = Create((SupportJobId)raw);
        return true;
    }

    public IEnumerable<SupportJob> All() => all.Value;
}
