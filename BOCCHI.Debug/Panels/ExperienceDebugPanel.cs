using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.UI;
using BOCCHI.Experience.Services;
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

        TrackerRateRenderer.RenderPerHour(
            ui,
            "Experience Per Hour",
            tracker.ExperiencePerHour,
            tracker.GetExperienceHistory(TimeSpan.FromSeconds(config.GraphBucketSize)),
            "##debug_xp_history",
            plotHeight: 60f
        );
    }
}
