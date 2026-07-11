using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Currency.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.UI;

namespace BOCCHI.Currency;

public class CurrencyRenderer(
    ICurrencyTracker tracker,
    CurrencyConfig config,
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

        ui.LabelledValue("Gold Per Hour", tracker.GoldPerHour.ToString("f2"));
        if (uiConfig.ShowCurrencyTrackerGraph)
        {
            TrackerPlotHelper.PlotPerHourHistory(tracker.GetGoldHistory(graphBucketSize), "##gold_history");
        }

        ui.LabelledValue("Silver Per Hour", tracker.SilverPerHour.ToString("f2"));
        if (uiConfig.ShowCurrencyTrackerGraph)
        {
            TrackerPlotHelper.PlotPerHourHistory(tracker.GetSilverHistory(graphBucketSize), "##silver_history");
        }
    }

    public bool ShouldRender()
    {
        return uiConfig.ShowCurrencyTracker;
    }
}
