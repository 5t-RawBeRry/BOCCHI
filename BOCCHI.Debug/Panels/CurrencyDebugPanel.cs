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

        ui.LabelledValue("Gold Per Hour", tracker.GoldPerHour.ToString("f2"));
        TrackerPlotHelper.PlotPerHourHistory(tracker.GetGoldHistory(bucketSize), "##debug_gold_history", height: 60f);

        ui.LabelledValue("Silver Per Hour", tracker.SilverPerHour.ToString("f2"));
        TrackerPlotHelper.PlotPerHourHistory(tracker.GetSilverHistory(bucketSize), "##debug_silver_history", height: 60f);
    }
}
