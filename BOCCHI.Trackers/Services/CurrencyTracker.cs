using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Zones;
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

public class CurrencyTracker(IZoneProvider zones) : ICurrencyTracker, IOnUpdate, IOnTerritoryChanged
{
    private readonly DeltaRateTracker goldTracker = new(() => DeltaRateTracker.DefaultWindow);

    private readonly DeltaRateTracker silverTracker = new(() => DeltaRateTracker.DefaultWindow);

    private bool inOccultCrescent;

    public double GoldPerHour => goldTracker.PerHour;

    public double SilverPerHour => silverTracker.PerHour;

    public float[] GetGoldHistory(TimeSpan sampleDuration) => goldTracker.GetHistory(sampleDuration);

    public float[] GetSilverHistory(TimeSpan sampleDuration) => silverTracker.GetHistory(sampleDuration);

    public int Order => 0;

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
            goldTracker.SetCounting(false);
            silverTracker.SetCounting(false);
            return;
        }

        int gold = GetCurrentGold();
        int silver = GetCurrentSilver();

        // Inventory can read 0 while bags are still loading — don't treat that as a spend.
        if ((gold == 0 && goldTracker.HasValue && goldTracker.LastValue > 0)
            || (silver == 0 && silverTracker.HasValue && silverTracker.LastValue > 0))
        {
            goldTracker.SetCounting(false);
            silverTracker.SetCounting(false);
            return;
        }

        goldTracker.SetCounting(true);
        silverTracker.SetCounting(true);

        if (AddonHelpers.IsShopExchangeOpen())
        {
            goldTracker.SyncBaseline(gold);
            silverTracker.SyncBaseline(silver);
            return;
        }

        goldTracker.RecordPositiveDelta(gold);
        silverTracker.RecordPositiveDelta(silver);
    }

    private void ApplyZone(bool inOc)
    {
        if (inOc == inOccultCrescent)
        {
            return;
        }

        inOccultCrescent = inOc;
        goldTracker.Reset();
        silverTracker.Reset();
    }

    private static int GetCurrentGold() => OccultCrescentHelper.GetGoldTotal();

    private static int GetCurrentSilver() => OccultCrescentHelper.GetSilverTotal();
}
