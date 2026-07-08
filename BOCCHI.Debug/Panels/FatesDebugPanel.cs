using BOCCHI.Common;
using BOCCHI.Common.Services;
using Dalamud.Bindings.ImGui;
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
            ui.Text(fate.Name, branding.DalamudYellow);
            ImGui.Indent(32);

            ui.LabelledValue("Id", fate.Id);
            ui.LabelledValue("Position", fate.Position.ToString("f2"));
            ui.LabelledValue("State", fate.State);
            ui.LabelledValue("Progress", fate.Progress);
            ui.LabelledValue("Radius", fate.Radius);

            ImGui.Unindent(32);
        }
    }
}
