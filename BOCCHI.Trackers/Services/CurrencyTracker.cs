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

public class CurrencyTracker(UIConfig config) : ICurrencyTracker, IOnUpdate, IOnTerritoryChanged
{
    private readonly DeltaRateTracker goldTracker = new(() => TimeSpan.FromMinutes(config.TrackedDuration));

    private readonly DeltaRateTracker silverTracker = new(() => TimeSpan.FromMinutes(config.TrackedDuration));

    private bool needsBaseline = true;

    private bool stateWasAvailable;

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

    public void OnTerritoryChanged(uint territory)
    {
        // Re-baseline after instance / zone changes so a late OC silver read is not a huge “gain”.
        needsBaseline = true;
        stateWasAvailable = false;
    }

    public void Update()
    {
        if (!OccultCrescentHelper.IsStateAvailable())
        {
            stateWasAvailable = false;
            return;
        }

        int gold = GetCurrentGold();
        int silver = GetCurrentSilver();

        // First OC-ready sample after unavailable / territory change — never count as income.
        if (needsBaseline || !stateWasAvailable || !goldTracker.HasValue || !silverTracker.HasValue)
        {
            goldTracker.Reset();
            silverTracker.Reset();
            goldTracker.SyncBaseline(gold);
            silverTracker.SyncBaseline(silver);
            needsBaseline = false;
            stateWasAvailable = true;
            return;
        }

        // Shopping churns currency reads; re-baseline so spend→recover isn't a false gain.
        if (AddonHelpers.IsShopExchangeOpen())
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
