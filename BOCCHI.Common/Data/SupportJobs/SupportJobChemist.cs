using BOCCHI.Common.Services;

namespace BOCCHI.Common.Data.SupportJobs;

public static class SupportJobChemist
{
    public static bool IsUnlocked(ISupportJobFactory supportJobs) =>
        supportJobs.Create(SupportJobId.PhantomChemist).Level >= 1;
}
