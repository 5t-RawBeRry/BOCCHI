using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.CriticalEncounters;

public class CriticalEncountersRenderer(
    ICriticalEncounterRepository criticalEncounters,
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
        var snapshots = criticalEncounters.SnapshotWithoutForkedTower().ToList();
        if (snapshots.Count == 0)
        {
            ui.Text(translator.T(".world.critical_encounters.none"));
            return;
        }

        using var list = ImGuiSectionHelper.BoundedList("##ce_list", 120f);
        if (!list.IsOpen)
        {
            return;
        }

        foreach (var criticalEncounter in snapshots)
        {
            var details =
                $"{criticalEncounter.State} · #{criticalEncounter.Id.Value} · {criticalEncounter.Position:f0}";

            ActivitySnapshotRenderer.RenderCompact(
                ui,
                branding.DalamudYellow,
                branding.DalamudGrey,
                criticalEncounter.Name,
                details);
        }
    }

    public bool ShouldRender()
    {
        return uiConfig.ShowWorldSection;
    }
}
