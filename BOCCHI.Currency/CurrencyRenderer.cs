using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Currency.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.UI;

namespace BOCCHI.Currency;

public class CurrencyRenderer(
    ICurrencyTracker tracker,
    TrackerConfig config,
    UIConfig uiConfig,
    IUIService ui
) : IDynamicRenderer
{
    public void Render()
    {
        if (!uiConfig.ShowCurrencyTracker)
        {
            return;
        }

        var graphBucketSize = TimeSpan.FromSeconds(config.GraphBucketSize);

        TrackerRateRenderer.RenderPerHour(
            ui,
            "Gold Per Hour",
            tracker.GoldPerHour,
            tracker.GetGoldHistory(graphBucketSize),
            "##gold_history",
            uiConfig.ShowCurrencyTrackerGraph
        );

        TrackerRateRenderer.RenderPerHour(
            ui,
            "Silver Per Hour",
            tracker.SilverPerHour,
            tracker.GetSilverHistory(graphBucketSize),
            "##silver_history",
            uiConfig.ShowCurrencyTrackerGraph
        );
    }

    public bool ShouldRender()
    {
        return uiConfig.ShowCurrencyTracker;
    }
}
