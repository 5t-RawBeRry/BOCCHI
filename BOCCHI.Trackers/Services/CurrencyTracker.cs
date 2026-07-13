using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
using BOCCHI.Common.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Currency.Services;

public class CurrencyTracker(TrackerConfig config) : ICurrencyTracker, IOnUpdate
{
    private readonly DeltaRateTracker goldTracker = new(() => TimeSpan.FromMinutes(config.TrackedDuration));

    private readonly DeltaRateTracker silverTracker = new(() => TimeSpan.FromMinutes(config.TrackedDuration));

    public UpdateLimit UpdateLimit
    {
        get => new()
        {
            Mode = UpdateLimitMode.Milliseconds,
            Limit = 250,
        };
    }

    public double GoldPerHour => goldTracker.PerHour;

    public double SilverPerHour => silverTracker.PerHour;

    public float[] GetGoldHistory(TimeSpan sampleDuration)
    {
        return goldTracker.GetHistory(sampleDuration);
    }

    public float[] GetSilverHistory(TimeSpan sampleDuration)
    {
        return silverTracker.GetHistory(sampleDuration);
    }

    public void Update()
    {
        if (!OccultCrescentHelper.IsStateAvailable())
        {
            return;
        }

        var gold = GetCurrentGold();
        var silver = GetCurrentSilver();

        if (!goldTracker.HasValue && !silverTracker.HasValue)
        {
            goldTracker.SyncBaseline(gold);
            silverTracker.SyncBaseline(silver);
            return;
        }

        goldTracker.RecordPositiveDelta(gold);
        silverTracker.RecordPositiveDelta(silver);
    }

    private static int GetCurrentGold()
    {
        return OccultCrescentHelper.GetGold();
    }

    private static int GetCurrentSilver()
    {
        return OccultCrescentHelper.GetSilver();
    }
}
