using BOCCHI.Common;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.UI;

namespace BOCCHI.Fates;

public class FatesRenderer(
    IFateRepository fates,
    IBrandingService branding,
    IUIService ui
) : IDynamicRenderer
{
    public void Render()
    {
        foreach (var fate in fates.Snapshot())
        {
            ActivitySnapshotRenderer.Render(
                ui,
                branding.DalamudYellow,
                fate.Name,
                null,
                ("Id", fate.Id),
                ("Position", fate.Position.ToString("f2")),
                ("State", fate.State),
                ("Radius", fate.Radius)
            );
        }
    }

    public bool ShouldRender()
    {
        return true;
    }
}
