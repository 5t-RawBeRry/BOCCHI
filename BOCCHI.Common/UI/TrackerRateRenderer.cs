using Dalamud.Bindings.ImGui;
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
            PlotPerHourHistory(history, plotId, plotHeight);
        }
    }

    public static void PlotPerHourHistory(float[] history, string id, float height = 30f)
    {
        if (history.Length <= 0)
        {
            return;
        }

        float max = history.Max();
        if (max <= 0f)
        {
            max = 1f;
        }

        ImGui.PlotLines(
            id,
            history.AsSpan(),
            history.Length,
            string.Empty,
            0f,
            max,
            new(ImGui.GetContentRegionAvail().X, height)
        );
    }
}
