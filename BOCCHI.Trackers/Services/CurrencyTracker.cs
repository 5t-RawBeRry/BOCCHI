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

    /// <summary>A state dropout shorter than this is a blip, not a session gap — keep recording.</summary>
    private static readonly TimeSpan MaxIgnorableGap = TimeSpan.FromSeconds(5);

    private DateTime lastAvailableUtc = DateTime.MinValue;

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
        // A different zone is a different session, so the samples so far no longer describe it.
        // This is the only place history should be dropped — see Update.
        goldTracker.Reset();
        silverTracker.Reset();
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

        // Re-anchoring costs us whatever arrived during the gap, so only do it for a gap long
        // enough to have hidden a real jump. A momentary read of "state unavailable" used to be
        // enough, which silently swallowed gains — the experience tracker has no such branch, it
        // simply resumes recording, which is why it ticks on every kill and this did not.
        bool longGap = lastAvailableUtc == DateTime.MinValue
                       || DateTime.UtcNow - lastAvailableUtc > MaxIgnorableGap;
        lastAvailableUtc = DateTime.UtcNow;

        if (needsBaseline || (!stateWasAvailable && longGap) || !goldTracker.HasValue || !silverTracker.HasValue)
        {
            goldTracker.SyncBaseline(gold);
            silverTracker.SyncBaseline(silver);
            needsBaseline = false;
            stateWasAvailable = true;
            return;
        }

        stateWasAvailable = true;

        // Vendor spend is not a session gain.
        if (AddonHelpers.IsShopExchangeOpen())
        {
            goldTracker.SyncBaseline(gold);
            silverTracker.SyncBaseline(silver);
            return;
        }

        goldTracker.RecordPositiveDelta(gold);
        silverTracker.RecordPositiveDelta(silver);
    }

    private static int GetCurrentGold() => OccultCrescentHelper.GetGoldTotal();

    private static int GetCurrentSilver() => OccultCrescentHelper.GetSilverTotal();
}
