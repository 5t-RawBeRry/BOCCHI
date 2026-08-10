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
    ICarrotHunter carrotHunter,
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
        DrawCarrotHuntPanel();
        DrawNearbyTreasures();
    }

    public bool ShouldRender() => uiConfig.ShowTreasureSection;

    private void DrawHuntPanel()
    {
        ImGui.Spacing();

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

        if (hunter.ManagedByIllegalModeFiller)
        {
            ImGui.TextWrapped(translator.T(".treasure.managed_by_illegal_mode"));
            if (hunter.Elapsed > TimeSpan.Zero)
            {
                ui.LabelledValue(translator.T(".treasure.elapsed"), $"{hunter.Elapsed:mm\\:ss}");
            }

            TreasureHuntStatusUi.DrawProgress(hunter, ui, translator, config);
            return;
        }

        // Carrot Hunt owns the section — don't offer Start Hunt beside an active carrot run.
        if (carrotHunter.Running)
        {
            ImGui.TextWrapped(translator.T(".treasure.managed_by_carrot"));
            return;
        }

        if (!hunter.Running)
        {
            if (ImGui.Button(translator.T(".treasure.start_hunt")))
            {
                hunter.Toggle();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(translator.T(".treasure.start_hunt_tooltip"));
            }

            // Idle: Start Treasure Hunt | Start Carrot Hunt on one row.
            if (carrotHunter.IsVnavAvailable && carrotHunter.IsVnavReady)
            {
                ImGui.SameLine();
                if (ImGui.Button(translator.T(".treasure.start_carrot_hunt")))
                {
                    carrotHunter.Toggle();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(translator.T(".treasure.carrot_hunt_description"));
                }
            }

            return;
        }

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

        ui.LabelledValue(translator.T(".treasure.elapsed"), $"{hunter.Elapsed:mm\\:ss}");
        TreasureHuntStatusUi.DrawProgress(hunter, ui, translator, config);
    }

    private void DrawCarrotHuntPanel()
    {
        // Hide while a coffer hunt owns the section (standalone, Pots, or Illegal filler).
        if (hunter.Running || hunter.ManagedByPotsTreasure || hunter.ManagedByIllegalModeFiller)
        {
            return;
        }

        bool startsSharedWithHuntRow = !carrotHunter.Running
                                       && hunter.IsVnavAvailable
                                       && hunter.IsVnavReady
                                       && carrotHunter.IsVnavAvailable
                                       && carrotHunter.IsVnavReady;

        bool showCarrotStatus = carrotHunter.Running || carrotHunter.Elapsed > TimeSpan.Zero;
        bool showUseCarrot = showCarrotStatus || carrotHunter.FortuneCarrotsRemaining > 0;

        // Both idle: start buttons already sit on the hunt row — skip empty Carrot section.
        if (startsSharedWithHuntRow && !showCarrotStatus && !showUseCarrot)
        {
            return;
        }

        ImGui.Separator();
        ui.Text(translator.T(".treasure.carrot_hunt_title"));
        ImGui.TextWrapped(translator.T(".treasure.carrot_hunt_description"));

        if (!carrotHunter.IsVnavAvailable)
        {
            ImGui.TextUnformatted(translator.T(".treasure.requires_vnav"));
            return;
        }

        if (!carrotHunter.IsVnavReady)
        {
            ImGui.TextUnformatted(translator.T(".treasure.waiting_navmesh"));
            return;
        }

        if (carrotHunter.Running)
        {
            if (ImGui.Button(translator.T(".treasure.stop_carrot_hunt")))
            {
                carrotHunter.Toggle();
            }
        }
        else if (!startsSharedWithHuntRow
                 && ImGui.Button(translator.T(".treasure.start_carrot_hunt")))
        {
            carrotHunter.Toggle();
        }

        if (showCarrotStatus)
        {
            ui.LabelledValue(translator.T(".treasure.elapsed"), $"{carrotHunter.Elapsed:mm\\:ss}");
            ui.LabelledValue(
                translator.T(".treasure.carrot_hunt_phase"),
                translator.T($".treasure.carrot_hunt_phases.{carrotHunter.Phase.ToString().ToSnakeCase()}"));
            ui.LabelledValue(
                translator.T(".treasure.fortune_carrots"),
                carrotHunter.FortuneCarrotsRemaining.ToString());
        }

        if (showUseCarrot)
        {
            ImGui.BeginDisabled(carrotHunter.FortuneCarrotsRemaining <= 0);
            if (ImGui.Button(translator.T(".treasure.use_fortune_carrot")))
            {
                carrotHunter.UseFortuneCarrot();
            }

            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(translator.T(".treasure.use_fortune_carrot_tooltip"));
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

        List<TreasureCoffer> treasures = tracker.Treasures
            .Where(t => t.IsValid())
            .OrderBy(t => player.Position.Distance(t.GetPosition()))
            .ToList();

        // Content-sized child (same pattern as FATE/CE lists) — no empty padding for short lists.
        using ImGuiSectionHelper.BoundedListScope list =
            ImGuiSectionHelper.BoundedList("##nearby_treasures", treasures.Count, maxHeight: 120f);
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
