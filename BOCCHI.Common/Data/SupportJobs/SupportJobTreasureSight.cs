namespace BOCCHI.Common.Data.SupportJobs;

public static class SupportJobTreasureSight
{
    public const byte RequiredFreelancerLevel = 10;

    public static bool CanCast(ISupportJobFactory supportJobs) =>
        supportJobs.Create(SupportJobId.PhantomFreelancer).Level >= RequiredFreelancerLevel;
}
