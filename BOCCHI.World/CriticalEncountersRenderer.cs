using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
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
        List<CriticalEncounter> snapshots = criticalEncounters.SnapshotWithoutForkedTower().ToList();
        if (snapshots.Count == 0)
        {
            ui.Text(translator.T(".world.critical_encounters.none"));
            return;
        }

        using ImGuiSectionHelper.BoundedListScope list = ImGuiSectionHelper.BoundedList("##ce_list", 120f);
        if (!list.IsOpen)
        {
            return;
        }

        foreach(CriticalEncounter criticalEncounter in snapshots)
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

    public bool ShouldRender() => uiConfig.ShowWorldSection;
}
