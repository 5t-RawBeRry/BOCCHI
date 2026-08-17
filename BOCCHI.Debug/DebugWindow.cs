using BOCCHI.Common.Data.Zones;
using Dalamud.Bindings.ImGui;
using Ocelot.Services.UI;
using Ocelot.Windows;
using System.Numerics;

namespace BOCCHI.Debug;

public interface IDebugWindow : IWindow;

public sealed class DebugWindow
(
    IEnumerable<IDebugPanel> panels,
    IZoneProvider zones,
    IUIService ui
) : OcelotWindow("BOCCHI Debug"), IDebugWindow
{
    private readonly IDebugPanel[] panels = panels.ToArray();

    private int selectedPanelIndex;

    protected override void Render()
    {
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            ui.Text("Not in a supported Occult Crescent zone.");
            return;
        }

        if (panels.Length == 0)
        {
            ui.Text("No debug panels registered.");
            return;
        }

        float panelWidth = 200f;
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        ImGui.BeginGroup();

        ImGui.BeginChild("PanelList", new(panelWidth, 0), true);
        for(int i = 0; i < panels.Length; i++)
        {
            if (ImGui.Selectable(panels[i].Name, i == selectedPanelIndex))
            {
                selectedPanelIndex = i;
            }
        }

        ImGui.EndChild();

        ImGui.SameLine(0, spacing);

        ImGui.BeginGroup();
        ImGui.BeginChild("PanelContent", Vector2.Zero);
        panels[selectedPanelIndex].Render();
        ImGui.EndChild();
        ImGui.EndGroup();

        ImGui.EndGroup();
    }
}
