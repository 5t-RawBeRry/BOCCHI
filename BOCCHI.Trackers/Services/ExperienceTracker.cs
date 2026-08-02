using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Experience.Services;

public interface IExperienceTracker
{
    double ExperiencePerHour { get; }

    float[] GetExperienceHistory(TimeSpan sampleDuration);
}

public class ExperienceTracker
(
    TrackerConfig config,
    ISupportJobFactory supportJobs
) : IExperienceTracker, IOnUpdate
{
    private readonly DeltaRateTracker tracker = new(() => TimeSpan.FromMinutes(config.TrackedDuration));

    public double ExperiencePerHour => tracker.PerHour;

    public float[] GetExperienceHistory(TimeSpan sampleDuration) => tracker.GetHistory(sampleDuration);

    public UpdateLimit UpdateLimit =>
        new()
        {
            Mode = UpdateLimitMode.Milliseconds,
            Limit = 250
        };

    public void Update()
    {
        if (!OccultCrescentHelper.IsStateAvailable())
        {
            return;
        }

        tracker.RecordPositiveDelta(GetCurrentTotalExperience());
    }

    private long GetCurrentTotalExperience()
    {
        return supportJobs.All().Sum(j => j.TotalExperience);
    }
}
