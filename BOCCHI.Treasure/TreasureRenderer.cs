using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Treasure.Hunt;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;
using Ocelot.Services.UI;

namespace BOCCHI.Treasure;

public class TreasureRenderer(
    ITreasureTracker tracker,
    ITreasureHunter hunter,
    TreasureConfig config,
    IPlayer player,
    IUIService ui
) : IDynamicRenderer
{
    public uint Order => 30;

    public void Render()
    {
        if (!config.Enabled)
        {
            return;
        }

        ui.Text("Treasure");
        ImGui.Indent();

        DrawActiveChests();
        DrawHuntPanel();

        if (tracker.Treasures.Count <= 0)
        {
            ImGui.TextUnformatted("No nearby Treasure.");
            ImGui.Unindent();
            return;
        }

        foreach (var treasure in tracker.Treasures)
        {
            if (!treasure.IsValid())
            {
                continue;
            }

            var pos = treasure.GetPosition();
            ImGui.TextUnformatted(treasure.GetName());
            ImGui.Indent();
            ImGui.TextUnformatted($"({pos.X:F2}, {pos.Y:F2}, {pos.Z:F2})");
            ImGui.TextUnformatted($"({player.Position.Distance(pos):F2})");
            ImGui.Unindent();
        }

        ImGui.Unindent();
    }

    public bool ShouldRender()
    {
        return config.Enabled;
    }

    private void DrawHuntPanel()
    {
        if (!config.EnableTreasureHunt)
        {
            return;
        }

        ImGui.Separator();
        ui.Text("Treasure Hunter");

        if (!hunter.IsVnavReady)
        {
            ImGui.TextUnformatted("Requires vnavmesh.");
            return;
        }

        if (ImGui.Button(hunter.Running ? "Stop" : "Start"))
        {
            hunter.Toggle();
        }

        if (hunter.Elapsed > TimeSpan.Zero)
        {
            ui.LabelledValue("Elapsed", $"{hunter.Elapsed:mm\\:ss}");
        }

        if (hunter.Running && hunter.StepCount > 0)
        {
            ui.LabelledValue("Progress", $"{hunter.StepIndex}/{hunter.StepCount}");

            var current = hunter.GetCurrentStep();
            if (current?.Type == HuntPathfinderStepType.WalkToNode)
            {
                ui.LabelledValue("Distance to chest", $"{hunter.StepDistance:F2}/{config.HuntDetectionRange:F2}");
            }
        }
    }

    private void DrawActiveChests()
    {
        if (!tracker.CountInitialised)
        {
            return;
        }

        ui.LabelledValue("Active Bronze", $"{tracker.BronzeChests}/30");
        if (config.ShowPercentageActiveTreasureCount)
        {
            var percentage = tracker.BronzeChests / 30f * 100f;
            ImGui.SameLine();
            ImGui.TextUnformatted($"({percentage:F2}%)");
        }

        ui.LabelledValue("Active Silver", $"{tracker.SilverChests}/8");
        if (config.ShowPercentageActiveTreasureCount)
        {
            var percentage = tracker.SilverChests / 8f * 100f;
            ImGui.SameLine();
            ImGui.TextUnformatted($"({percentage:F2}%)");
        }
    }
}
