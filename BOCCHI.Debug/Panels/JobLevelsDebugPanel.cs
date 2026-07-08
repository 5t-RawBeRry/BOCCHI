using BOCCHI.Common;
using BOCCHI.Common.Data.SupportJobs;
using Ocelot.Services.UI;

namespace BOCCHI.Debug.Panels;

public sealed class JobLevelsDebugPanel(
    ISupportJobFactory supportJobs,
    IBrandingService branding,
    IUIService ui
) : IDebugPanel
{
    public string Name => "Job Levels";

    public void Render()
    {
        foreach (var job in supportJobs.All())
        {
            ui.Text(job.Data.Name.ToString(), branding.DalamudYellow);
            ui.LabelledValue("Level", $"{job.Level}/{job.Data.LevelMax}");
            ui.LabelledValue("Experience", job.CurrentExperience);
        }
    }
}
