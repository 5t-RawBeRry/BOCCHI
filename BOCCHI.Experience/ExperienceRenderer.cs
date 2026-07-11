using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.UI;
using BOCCHI.Experience.Services;
using Ocelot.Services.UI;

namespace BOCCHI.Experience;

public class ExperienceRenderer(
    IExperienceTracker tracker,
    ExperienceConfig config,
    UIConfig uiConfig,
    ISupportJobFactory supportJobs,
    IBrandingService branding,
    IUIService ui
) : IDynamicRenderer
{
    public void Render()
    {
        if (!supportJobs.TryGetCurrent(out var current))
        {
            ui.Text("No jobs found", branding.DalamudRed);
            return;
        }

        var left = ui.Compose()
            .Text("Current Job", branding.DalamudYellow)
            .Text(current.Data.Name.ToString());

        var right = ui.Compose()
            .Text($"Level: {current.Level} ({current.TotalExperience})");

        ui.Render(left, right);

        TrackerRateRenderer.RenderPerHour(
            ui,
            "Experience Per Hour",
            tracker.ExperiencePerHour,
            tracker.GetExperienceHistory(TimeSpan.FromSeconds(config.GraphBucketSize)),
            "##xp_history",
            uiConfig.ShowExperienceTrackerGraph
        );
    }

    public bool ShouldRender()
    {
        return uiConfig.ShowExperienceTracker;
    }
}
