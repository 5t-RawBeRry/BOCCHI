using System;
using System.Numerics;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Experience.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Services.UI;

namespace BOCCHI.Debug.Panels;

public sealed class ExperienceDebugPanel(
    IExperienceTracker tracker,
    ExperienceConfig config,
    ISupportJobFactory supportJobs,
    IBrandingService branding,
    IUIService ui
) : IDebugPanel
{
    public string Name => "Experience";

    public void Render()
    {
        if (!supportJobs.TryGetCurrent(out var current))
        {
            ui.Text("No support job found.", branding.DalamudRed);
            return;
        }

        ui.LabelledValue("Current Job", current.Data.Name.ToString());
        ui.LabelledValue("Level", $"{current.Level} ({current.TotalExperience})");
        ui.LabelledValue("Experience Per Hour", tracker.ExperiencePerHour.ToString("f2"));

        var history = tracker.GetExperienceHistory(TimeSpan.FromSeconds(config.GraphBucketSize));
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
            "##debug_xp_history",
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
