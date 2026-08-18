using BOCCHI.Common.Data;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Experience.Services;

public interface IExperienceTracker
{
    double ExperiencePerHour { get; }

    float[] GetExperienceHistory(TimeSpan sampleDuration);
}

public class ExperienceTracker(ISupportJobFactory supportJobs, IZoneProvider zones)
    : IExperienceTracker, IOnUpdate, IOnTerritoryChanged
{
    private readonly DeltaRateTracker tracker = new(() => DeltaRateTracker.DefaultWindow);

    private bool inOccultCrescent;

    public double ExperiencePerHour => tracker.PerHour;

    public float[] GetExperienceHistory(TimeSpan sampleDuration) => tracker.GetHistory(sampleDuration);

    public UpdateLimit UpdateLimit =>
        new()
        {
            Mode = UpdateLimitMode.Milliseconds,
            Limit = 250
        };

    public void OnTerritoryChanged(uint territory) => ApplyZone(zones.GetZone().IsOccultCrescentZone());

    public void Update()
    {
        ApplyZone(zones.GetZone().IsOccultCrescentZone());
        if (!inOccultCrescent)
        {
            return;
        }

        if (!OccultCrescentHelper.IsStateAvailable())
        {
            tracker.SetCounting(false);
            return;
        }

        long total = GetCurrentTotalExperience();
        if (total == 0 && tracker.HasValue && tracker.LastValue > 0)
        {
            tracker.SetCounting(false);
            return;
        }

        tracker.SetCounting(true);
        tracker.RecordPositiveDelta(total);
    }

    private void ApplyZone(bool inOc)
    {
        if (inOc == inOccultCrescent)
        {
            return;
        }

        inOccultCrescent = inOc;
        tracker.Reset();
    }

    private long GetCurrentTotalExperience()
    {
        return supportJobs.All().Sum(j => j.TotalExperience);
    }
}
