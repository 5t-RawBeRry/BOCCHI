using BOCCHI.Common.Data.SupportJobs;

namespace BOCCHI.Common.Data.StateMemory;

public class BuffSupportJobMemory(SupportJobId job)
{
    public readonly SupportJobId Job = job;
}

public class TreasureSightSupportJobMemory(SupportJobId job)
{
    public readonly SupportJobId Job = job;
}
