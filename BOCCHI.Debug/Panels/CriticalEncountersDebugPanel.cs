using System;
using BOCCHI.Common;
using BOCCHI.Common.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Services.UI;

namespace BOCCHI.Debug.Panels;

public sealed class CriticalEncountersDebugPanel(
    ICriticalEncounterRepository criticalEncounters,
    IBrandingService branding,
    IUIService ui
) : IDebugPanel
{
    public string Name => "Critical Encounters";

    public void Render()
    {
        foreach (var encounter in criticalEncounters.Snapshot())
        {
            ui.Text(encounter.Name, branding.DalamudYellow);
            ImGui.SameLine();
            ImGui.TextUnformatted(FormatState(encounter.State, encounter.Progress));

            ImGui.Indent(32);

            ui.LabelledValue("Id", encounter.Id);
            ui.LabelledValue("Position", encounter.Position.ToString("f2"));
            ui.LabelledValue("Radius", encounter.Radius);

            ImGui.Unindent(32);
        }
    }

    private static string FormatState(DynamicEventState state, byte progress)
    {
        return state switch
        {
            DynamicEventState.Inactive => "(Inactive)",
            DynamicEventState.Register => "(Preparing)",
            DynamicEventState.Warmup => "(Starting)",
            DynamicEventState.Battle => $"({progress}%)",
            _ => $"({state})",
        };
    }
}
