using System;
using System.Numerics;
using BOCCHI.Common.Config;
using BOCCHI.Currency.Services;
using Dalamud.Bindings.ImGui;
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
        RenderGraph(tracker.GetGoldHistory(bucketSize), "##debug_gold_history");

        ui.LabelledValue("Silver Per Hour", tracker.SilverPerHour.ToString("f2"));
        RenderGraph(tracker.GetSilverHistory(bucketSize), "##debug_silver_history");
    }

    private static void RenderGraph(float[] history, string id)
    {
        if (history.Length <= 0)
        {
            return;
        }

        var max = history.Max();
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
            new Vector2(ImGui.GetContentRegionAvail().X, 60),
            sizeof(float)
        );
    }
}
