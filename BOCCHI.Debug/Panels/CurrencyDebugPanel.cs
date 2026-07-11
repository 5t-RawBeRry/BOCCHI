using BOCCHI.Common.Config;
using BOCCHI.Common.UI;
using BOCCHI.Currency.Services;
using Ocelot.Services.UI;

namespace BOCCHI.Debug.Panels;

public sealed class CurrencyDebugPanel(
    ICurrencyTracker tracker,
    CurrencyConfig config,
    IUIService ui
) : IDebugPanel
{
    public string Name => "Currency";

    public void Render()
    {
        var bucketSize = TimeSpan.FromSeconds(config.GraphBucketSize);

        TrackerRateRenderer.RenderPerHour(
            ui,
            "Gold Per Hour",
            tracker.GoldPerHour,
            tracker.GetGoldHistory(bucketSize),
            "##debug_gold_history",
            plotHeight: 60f
        );

        TrackerRateRenderer.RenderPerHour(
            ui,
            "Silver Per Hour",
            tracker.SilverPerHour,
            tracker.GetSilverHistory(bucketSize),
            "##debug_silver_history",
            plotHeight: 60f
        );
    }
}
