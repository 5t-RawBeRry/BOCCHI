using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Currency.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Currency;

public class CurrencyRenderer(
    ICurrencyTracker tracker,
    TrackerConfig config,
    UIConfig uiConfig,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    public MainWindowSection Section => MainWindowSection.Trackers;

    public uint Order => 10;

    public void Render()
    {
        if (!uiConfig.ShowCurrencyTracker)
        {
            return;
        }

        var graphBucketSize = TimeSpan.FromSeconds(config.GraphBucketSize);

        TrackerRateRenderer.RenderPerHour(
            ui,
            translator.T(".trackers.currency.gold_per_hour"),
            tracker.GoldPerHour,
            tracker.GetGoldHistory(graphBucketSize),
            "##gold_history",
            uiConfig.ShowCurrencyTrackerGraph
        );

        TrackerRateRenderer.RenderPerHour(
            ui,
            translator.T(".trackers.currency.silver_per_hour"),
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
