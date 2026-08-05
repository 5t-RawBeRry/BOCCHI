using BOCCHI.Automator.Services;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.EventDrops;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using BOCCHI.Treasure;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Automator;

public class PotsTreasureRenderer
(
    Func<IPotsTreasureMode> potsTreasureFactory,
    ITreasureHunter hunter,
    TreasureConfig treasureConfig,
    UIConfig uiConfig,
    EventDropConfig eventDropConfig,
    EventDropIconRenderer eventDrops,
    IFateRepository fates,
    IActivityNavigation navigation,
    IPotCycleTracker potCycle,
    IZoneProvider zones,
    IDataManager data,
    IUIService ui,
    IBrandingService branding,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    private IPotsTreasureMode? potsTreasure;

    private IPotsTreasureMode PotsTreasure => potsTreasure ??= potsTreasureFactory();

    public MainWindowSection Section => MainWindowSection.PotsTreasure;

    public void Render()
    {
        ImGui.Spacing();

        if (ImGui.Button(PotsTreasure.Running
            ? translator.T(".automation.pots_treasure.stop")
            : translator.T(".automation.pots_treasure.start")))
        {
            PotsTreasure.Toggle();
        }

        ImGui.Spacing();
        ImGui.TextWrapped(translator.T(".automation.pots_treasure.description"));

        ImGui.Spacing();
        PotTimerUi.Draw(potCycle, zones, data, ui, translator, branding);

        DrawActivePotFates();

        if (!PotsTreasure.Running)
        {
            return;
        }

        ImGui.Spacing();
        ui.LabelledValue(
            translator.T(".automation.pots_treasure.phase"),
            translator.T($".automation.pots_treasure.phases.{PotsTreasure.Phase.ToString().ToSnakeCase()}"));

        if (PotsTreasure.Phase == PotsTreasurePhase.Hunting && !hunter.Running)
        {
            if (ImGui.Button(translator.T(".automation.pots_treasure.resume_treasure_hunt")))
            {
                PotsTreasure.ResumeTreasureHunt();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(translator.T(".automation.pots_treasure.resume_treasure_hunt_tooltip"));
            }
        }

        // Compact hunt status while this mode owns the treasure hunter.
        if (hunter.ManagedByPotsTreasure && (hunter.Running || hunter.Elapsed > TimeSpan.Zero))
        {
            ImGui.Spacing();
            if (hunter.Elapsed > TimeSpan.Zero)
            {
                ui.LabelledValue(translator.T(".treasure.elapsed"), $"{hunter.Elapsed:mm\\:ss}");
            }

            TreasureHuntStatusUi.DrawProgress(hunter, ui, translator, treasureConfig);
        }
    }

    private void DrawActivePotFates()
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            return;
        }

        List<Fate> potFates = fates.Snapshot()
            .Where(f => zone.IsPotFate(f.Id.Value))
            .ToList();

        ImGui.Spacing();
        ui.Text(translator.T(".automation.pots_treasure.active_fates"), branding.DalamudYellow);

        if (potFates.Count == 0)
        {
            ui.Text(translator.T(".automation.pots_treasure.no_active_fate"), branding.DalamudGrey);
            return;
        }

        bool southHorn = zone.ZoneId == ZoneId.SouthHorn;
        float dropExtra = southHorn && eventDropConfig.AnyEnabled
            ? EventDropIconRenderer.IconBoxSize + 4f
            : 0f;

        using ImGuiSectionHelper.BoundedListScope list =
            ImGuiSectionHelper.BoundedList("##pots_treasure_fates", potFates.Count, 120f, dropExtra);
        if (!list.IsOpen)
        {
            return;
        }

        foreach (Fate fate in potFates)
        {
            string details = $"{fate.State} {fate.Progress}% · #{fate.Id.Value}";
            if (fate.TimeRemainingSeconds > 0)
            {
                details += $" · {TimeSpan.FromSeconds(fate.TimeRemainingSeconds):mm\\:ss}";
            }

            ActivitySnapshotRenderer.RenderCompactWithActions(
                ui,
                navigation,
                branding.DalamudYellow,
                branding.DalamudGrey,
                fate.Name,
                details,
                fate.Position,
                $"pot_fate_{fate.Id.Value}");

            if (southHorn
                && SouthHornEventDrops.TryGetFate(fate.Id.Value, out EventDropInfo drops))
            {
                eventDrops.Render(fate.Id.Value, drops);
            }
        }
    }

    public bool ShouldRender() => uiConfig.ShowPotsTreasureSection;
}
