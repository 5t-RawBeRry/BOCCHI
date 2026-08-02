using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
using BOCCHI.Common.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Currency.Services;

public interface ICurrencyTracker
{
    double GoldPerHour { get; }

    double SilverPerHour { get; }

    float[] GetGoldHistory(TimeSpan sampleDuration);

    float[] GetSilverHistory(TimeSpan sampleDuration);
}

public class CurrencyTracker(TrackerConfig config) : ICurrencyTracker, IOnUpdate
{
    private readonly DeltaRateTracker goldTracker = new(() => TimeSpan.FromMinutes(config.TrackedDuration));

    private readonly DeltaRateTracker silverTracker = new(() => TimeSpan.FromMinutes(config.TrackedDuration));

    public double GoldPerHour => goldTracker.PerHour;

    public double SilverPerHour => silverTracker.PerHour;

    public float[] GetGoldHistory(TimeSpan sampleDuration) => goldTracker.GetHistory(sampleDuration);

    public float[] GetSilverHistory(TimeSpan sampleDuration) => silverTracker.GetHistory(sampleDuration);

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

        int gold = GetCurrentGold();
        int silver = GetCurrentSilver();

        if (!goldTracker.HasValue && !silverTracker.HasValue)
        {
            goldTracker.SyncBaseline(gold);
            silverTracker.SyncBaseline(silver);
            return;
        }

        goldTracker.RecordPositiveDelta(gold);
        silverTracker.RecordPositiveDelta(silver);
    }

    private static int GetCurrentGold() => OccultCrescentHelper.GetGold();

    private static int GetCurrentSilver() => OccultCrescentHelper.GetSilver();
}
