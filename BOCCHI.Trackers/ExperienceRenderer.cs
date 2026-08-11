using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.UI;
using BOCCHI.Experience.Services;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Services.UI.ComposableStrings;
using Ocelot.Windows;

namespace BOCCHI.Experience;

public class ExperienceRenderer
(
    IExperienceTracker tracker,
    UIConfig uiConfig,
    ISupportJobFactory supportJobs,
    IBrandingService branding,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    public MainWindowSection Section => MainWindowSection.Trackers;

    public void Render()
    {
        if (!supportJobs.TryGetCurrent(out SupportJob current))
        {
            ui.Text(translator.T(".trackers.experience.no_job"), branding.DalamudRed);
            return;
        }

        ComposableGroup left = ui.Compose()
            .Text(translator.T(".trackers.experience.current_job"), branding.DalamudYellow)
            .Text(current.Data.Name.ToString());

        ComposableGroup right = ui.Compose()
            .Text(string.Format(translator.T(".trackers.experience.level"), current.Level, current.TotalExperience));

        ui.Render(left, right);

        TrackerRateRenderer.RenderPerHour(
            ui,
            translator.T(".trackers.experience.per_hour"),
            tracker.ExperiencePerHour,
            tracker.GetExperienceHistory(TimeSpan.FromSeconds(uiConfig.GraphBucketSize)),
            "##xp_history",
            uiConfig.ShowExperienceTrackerGraph
        );
    }

    public bool ShouldRender() => uiConfig.ShowExperienceTracker;
}
