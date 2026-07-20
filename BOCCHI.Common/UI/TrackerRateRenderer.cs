using Ocelot.Services.UI;
namespace BOCCHI.Common.UI;

public static class TrackerRateRenderer
{
    public static void RenderPerHour(
        IUIService ui,
        string label,
        double perHour,
        float[] history,
        string plotId,
        bool showGraph = true,
        float plotHeight = 30f
    )
    {
        ui.LabelledValue(label, perHour.ToString("f2"));

        if (showGraph)
        {
            TrackerPlotHelper.PlotPerHourHistory(history, plotId, plotHeight);
        }
    }
}
