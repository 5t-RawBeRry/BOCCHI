using BOCCHI.Common;
using BOCCHI.Common.Services;
using Dalamud.Bindings.ImGui;
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
        var snapshot = fates.Snapshot();
        foreach (var fate in snapshot)
        {
            ui.Text(fate.Name, branding.DalamudYellow);
            ImGui.Indent(32);

            ui.LabelledValue("Id", fate.Id);
            ui.LabelledValue("Position", fate.Position.ToString("f2"));
            ui.LabelledValue("State", fate.State);
            ui.LabelledValue("Radius", fate.Radius);

            ImGui.Unindent(32);
        }
    }

    public bool ShouldRender()
    {
        return true;
    }
}
