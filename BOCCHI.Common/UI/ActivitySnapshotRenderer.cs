using Dalamud.Bindings.ImGui;
using Ocelot.Graphics;
using Ocelot.Services.UI;

namespace BOCCHI.Common.UI;

public static class ActivitySnapshotRenderer
{
    public static void RenderCompact(
        IUIService ui,
        Color? titleColor,
        Color? detailColor,
        string title,
        string details
    )
    {
        ui.Text(title, titleColor);
        ImGui.SameLine(0f, 6f);
        ui.Text(details, detailColor);
    }

    public static void Render(
        IUIService ui,
        Color? titleColor,
        string title,
        string? titleSuffix,
        params (string Label, object Value)[] fields
    )
    {
        ui.Text(title, titleColor);

        if (!string.IsNullOrEmpty(titleSuffix))
        {
            ImGui.SameLine();
            ImGui.TextUnformatted(titleSuffix);
        }

        ImGui.Indent(32);

        foreach((var label, var value) in fields)
        {
            ui.LabelledValue(label, value);
        }

        ImGui.Unindent(32);
    }
}
