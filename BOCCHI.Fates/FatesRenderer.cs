using BOCCHI.Common;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.UI;

namespace BOCCHI.Fates;

public class FatesRenderer(
    IFateRepository fates,
    IFateScorer fateScorer,
    IBrandingService branding,
    IUIService ui
) : IDynamicRenderer
{
    public void Render()
    {
        foreach (var fate in fates.Snapshot())
        {
            var score = fateScorer.Score(fate);

            ActivitySnapshotRenderer.Render(
                ui,
                branding.DalamudYellow,
                fate.Name,
                $"Score: {score}",
                ("Id", fate.Id),
                ("Position", fate.Position.ToString("f2")),
                ("State", fate.State),
                ("Progress", fate.Progress),
                ("Radius", fate.Radius)
            );
        }
    }

    public bool ShouldRender()
    {
        return true;
    }
}
