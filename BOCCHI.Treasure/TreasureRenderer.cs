using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;
using System.Numerics;

namespace BOCCHI.Treasure;

public class TreasureRenderer
(
    ITreasureTracker tracker,
    ITreasureHunter hunter,
    IActivityNavigation navigation,
    TreasureConfig config,
    UIConfig uiConfig,
    IPlayer player,
    IBrandingService branding,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    public MainWindowSection Section => MainWindowSection.Treasure;

    public void Render()
    {
        DrawActiveChests();
        DrawHuntPanel();
        DrawNearbyTreasures();
    }

    public bool ShouldRender() => uiConfig.ShowTreasureSection;

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

        if (hunter.ManagedByPotsTreasure)
        {
            ImGui.TextWrapped(translator.T(".treasure.managed_by_pots"));
            if (hunter.Elapsed > TimeSpan.Zero)
            {
                ui.LabelledValue(translator.T(".treasure.elapsed"), $"{hunter.Elapsed:mm\\:ss}");
            }

            TreasureHuntStatusUi.DrawProgress(hunter, ui, translator, config);
            return;
        }

        if (!hunter.Running)
        {
            if (ImGui.Button(translator.T(".treasure.start_hunt")))
            {
                hunter.Toggle();
            }
        }
        else
        {
            if (hunter.Paused)
            {
                if (ImGui.Button(translator.T(".treasure.resume_hunt")))
                {
                    hunter.Resume();
                }
            }
            else if (ImGui.Button(translator.T(".treasure.pause_hunt")))
            {
                hunter.Pause();
            }

            ImGui.SameLine();
            if (ImGui.Button(translator.T(".treasure.stop_hunt")))
            {
                hunter.Toggle();
            }
        }

        if (hunter.Elapsed > TimeSpan.Zero)
        {
            ui.LabelledValue(translator.T(".treasure.elapsed"), $"{hunter.Elapsed:mm\\:ss}");
        }

        TreasureHuntStatusUi.DrawProgress(hunter, ui, translator, config);
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

        List<TreasureCoffer> treasures = tracker.Treasures
            .Where(t => t.IsValid())
            .OrderBy(t => player.Position.Distance(t.GetPosition()))
            .ToList();

        using ImGuiSectionHelper.BoundedListScope list = ImGuiSectionHelper.BoundedList("##nearby_treasures", 200f);
        if (!list.IsOpen)
        {
            return;
        }

        foreach(TreasureCoffer treasure in treasures)
        {
            Vector3 pos = treasure.GetPosition();
            string name = treasure.GetName();
            string details =
                $"{string.Format(translator.T(".treasure.distance"), player.Position.Distance(pos))} · {pos:f0}";

            ActivitySnapshotRenderer.RenderCompactWithActions(
                ui,
                navigation,
                branding.DalamudYellow,
                branding.DalamudGrey,
                name,
                details,
                pos,
                $"treasure_{treasure.Id}",
                includeTeleport: false);
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
            float percentage = tracker.BronzeChests / 30f * 100f;
            ImGui.SameLine();
            ImGui.TextUnformatted($"({percentage:F2}%)");
        }

        ui.LabelledValue(translator.T(".treasure.active_silver"), $"{tracker.SilverChests}/8");
        if (config.ShowPercentageActiveTreasureCount)
        {
            float percentage = tracker.SilverChests / 8f * 100f;
            ImGui.SameLine();
            ImGui.TextUnformatted($"({percentage:F2}%)");
        }
    }
}
