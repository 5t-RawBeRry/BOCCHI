using Dalamud.Bindings.ImGui;

namespace BOCCHI.Common.UI;

public static class TrackerPlotHelper
{
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
