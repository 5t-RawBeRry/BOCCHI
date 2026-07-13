using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.UI;
using BOCCHI.Treasure.Hunt;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Treasure;

public class TreasureRenderer(
    ITreasureTracker tracker,
    ITreasureHunter hunter,
    TreasureConfig config,
    UIConfig uiConfig,
    IPlayer player,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    public MainWindowSection Section => MainWindowSection.Treasure;

    public void Render()
    {
        if (!config.Enabled)
        {
            return;
        }

        DrawActiveChests();
        DrawHuntPanel();
        DrawNearbyTreasures();
    }

    public bool ShouldRender()
    {
        return uiConfig.ShowTreasureSection && config.Enabled;
    }

    private void DrawHuntPanel()
    {
        if (!config.EnableTreasureHunt)
        {
            return;
        }

        ImGui.Separator();
        ui.Text(translator.T(".treasure.title"));

        if (!hunter.IsVnavAvailable)
        {
            ImGui.TextUnformatted(translator.T(".treasure.requires_vnav"));
            return;
        }

        if (!hunter.IsVnavReady)
        {
            ImGui.TextUnformatted(translator.T(".treasure.waiting_navmesh"));
            return;
        }

        if (ImGui.Button(hunter.Running
                ? translator.T(".treasure.stop_hunt")
                : translator.T(".treasure.start_hunt")))
        {
            hunter.Toggle();
        }

        if (hunter.Elapsed > TimeSpan.Zero)
        {
            ui.LabelledValue(translator.T(".treasure.elapsed"), $"{hunter.Elapsed:mm\\:ss}");
        }

        if (hunter.Running && hunter.StepCount > 0)
        {
            ui.LabelledValue(translator.T(".treasure.progress"), $"{hunter.StepIndex}/{hunter.StepCount}");

            var current = hunter.GetCurrentStep();
            if (current?.Type == HuntPathfinderStepType.WalkToNode)
            {
                ui.LabelledValue(
                    translator.T(".treasure.distance_to_chest"),
                    $"{hunter.StepDistance:F2}/{config.HuntDetectionRange:F2}");
            }
        }
    }

    private void DrawNearbyTreasures()
    {
        ImGui.Separator();
        ui.Text(translator.T(".treasure.nearby_title"));

        if (tracker.Treasures.Count <= 0)
        {
            ImGui.TextUnformatted(translator.T(".treasure.none_nearby"));
            return;
        }

        var treasures = tracker.Treasures
            .Where(t => t.IsValid())
            .OrderBy(t => player.Position.Distance(t.GetPosition()))
            .ToList();

        using var list = ImGuiSectionHelper.BoundedList("##nearby_treasures", 160f);
        if (!list.IsOpen)
        {
            return;
        }

        foreach (var treasure in treasures)
        {
            var pos = treasure.GetPosition();
            ImGui.TextUnformatted(treasure.GetName());
            ImGui.Indent();
            ImGui.TextUnformatted(string.Format(translator.T(".treasure.distance"), player.Position.Distance(pos)));
            ImGui.TextUnformatted(string.Format(translator.T(".treasure.position"), pos.X, pos.Y, pos.Z));
            ImGui.Unindent();
        }
    }

    private void DrawActiveChests()
    {
        if (!tracker.CountInitialised)
        {
            return;
        }

        ui.LabelledValue(translator.T(".treasure.active_bronze"), $"{tracker.BronzeChests}/30");
        if (config.ShowPercentageActiveTreasureCount)
        {
            var percentage = tracker.BronzeChests / 30f * 100f;
            ImGui.SameLine();
            ImGui.TextUnformatted($"({percentage:F2}%)");
        }

        ui.LabelledValue(translator.T(".treasure.active_silver"), $"{tracker.SilverChests}/8");
        if (config.ShowPercentageActiveTreasureCount)
        {
            var percentage = tracker.SilverChests / 8f * 100f;
            ImGui.SameLine();
            ImGui.TextUnformatted($"({percentage:F2}%)");
        }
    }
}
