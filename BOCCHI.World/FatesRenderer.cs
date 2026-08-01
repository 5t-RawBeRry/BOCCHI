using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Fates;

public class FatesRenderer
(
    IFateRepository fates,
    IFateScorer fateScorer,
    UIConfig uiConfig,
    IBrandingService branding,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    public MainWindowSection Section => MainWindowSection.World;

    public string? SubsectionTitle => translator.T(".world.fates.title");

    public void Render()
    {
        List<Fate> snapshots = fates.Snapshot().ToList();
        if (snapshots.Count == 0)
        {
            ui.Text(translator.T(".world.fates.none"));
            return;
        }

        using ImGuiSectionHelper.BoundedListScope list = ImGuiSectionHelper.BoundedList("##fates_list", 120f);
        if (!list.IsOpen)
        {
            return;
        }

        foreach(Fate fate in snapshots)
        {
            FateScore score = fateScorer.Score(fate);
            string details =
                $"Score {score:F1} · {fate.State} {fate.Progress}% · #{fate.Id.Value} · {fate.Position:f0} · r{fate.Radius}";

            ActivitySnapshotRenderer.RenderCompact(
                ui,
                branding.DalamudYellow,
                branding.DalamudGrey,
                fate.Name,
                details);
        }
    }

    public bool ShouldRender() => uiConfig.ShowWorldSection;
}
