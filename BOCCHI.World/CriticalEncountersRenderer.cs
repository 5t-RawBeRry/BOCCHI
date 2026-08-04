using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.CriticalEncounters;

public class CriticalEncountersRenderer
(
    ICriticalEncounterRepository criticalEncounters,
    IActivityNavigation navigation,
    IZoneProvider zones,
    ForkedTowerConfig forkedTowerConfig,
    UIConfig uiConfig,
    IBrandingService branding,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    public MainWindowSection Section => MainWindowSection.World;

    public string? SubsectionTitle => translator.T(".world.critical_encounters.title");

    public void Render()
    {
        RenderForkedTower(out bool showedTower);

        List<CriticalEncounter> snapshots = criticalEncounters.SnapshotWithoutForkedTower().ToList();
        if (snapshots.Count == 0)
        {
            if (!showedTower)
            {
                ui.Text(translator.T(".world.critical_encounters.none"));
            }

            return;
        }

        using ImGuiSectionHelper.BoundedListScope list = ImGuiSectionHelper.BoundedList("##ce_list", 120f);
        if (!list.IsOpen)
        {
            return;
        }

        foreach (CriticalEncounter criticalEncounter in snapshots)
        {
            string details =
                $"{criticalEncounter.State} · #{criticalEncounter.Id.Value} · {criticalEncounter.Position:f0}";

            // Match old panel: action buttons while registering / preparing.
            bool showActions = criticalEncounter.State is DynamicEventState.Register or DynamicEventState.Warmup
                               && criticalEncounter.Position is { X: not float.NaN };

            if (showActions)
            {
                ActivitySnapshotRenderer.RenderCompactWithActions(
                    ui,
                    navigation,
                    branding.DalamudYellow,
                    branding.DalamudGrey,
                    criticalEncounter.Name,
                    details,
                    criticalEncounter.Position,
                    $"ce_{criticalEncounter.Id.Value}");
            }
            else
            {
                ActivitySnapshotRenderer.RenderCompact(
                    ui,
                    branding.DalamudYellow,
                    branding.DalamudGrey,
                    criticalEncounter.Name,
                    details);
            }
        }
    }

    private void RenderForkedTower(out bool showed)
    {
        showed = false;
        if (!forkedTowerConfig.ShowRegistrationCountdown)
        {
            return;
        }

        if (zones.GetZone().ZoneId != ZoneId.SouthHorn)
        {
            return;
        }

        CriticalEncounter? tower = criticalEncounters.TryGetForkedTower();
        if (tower == null || tower.State == DynamicEventState.Battle)
        {
            return;
        }

        string details = tower.State switch
        {
            DynamicEventState.Register when tower.GetTimeUntilStart() is { } remaining =>
                remaining <= TimeSpan.Zero
                    ? $"{tower.State} · #{tower.Id.Value} · registering…"
                    : $"{tower.State} · #{tower.Id.Value} · {remaining.Minutes:D2}:{remaining.Seconds:D2}",
            DynamicEventState.Warmup => $"{tower.State} · #{tower.Id.Value} · warmup",
            DynamicEventState.Inactive => $"{tower.State} · #{tower.Id.Value}",
            var _ => $"{tower.State} · #{tower.Id.Value}"
        };

        ActivitySnapshotRenderer.RenderCompact(
            ui,
            branding.DalamudYellow,
            branding.DalamudGrey,
            tower.Name,
            details);
        showed = true;
    }

    public bool ShouldRender() => uiConfig.ShowWorldSection;
}
