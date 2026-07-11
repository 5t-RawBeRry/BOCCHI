using BOCCHI.Common;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.UI;

namespace BOCCHI.Debug.Panels;

public sealed class FatesDebugPanel(
    IFateRepository fates,
    IBrandingService branding,
    IUIService ui
) : IDebugPanel
{
    public string Name => "Fates";

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
                ("Progress", fate.Progress),
                ("Radius", fate.Radius)
            );
        }
    }
}
