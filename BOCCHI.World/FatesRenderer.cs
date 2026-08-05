using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.EventDrops;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
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
    IActivityNavigation navigation,
    IZoneProvider zones,
    UIConfig uiConfig,
    EventDropConfig eventDropConfig,
    EventDropIconRenderer eventDrops,
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

        bool southHorn = zones.GetZone().ZoneId == ZoneId.SouthHorn;
        float dropExtra = southHorn && eventDropConfig.AnyEnabled
            ? EventDropIconRenderer.IconBoxSize + 4f
            : 0f;
        float maxHeight = dropExtra > 0f ? 240f : 120f;

        using ImGuiSectionHelper.BoundedListScope list =
            ImGuiSectionHelper.BoundedList("##fates_list", snapshots.Count, maxHeight, dropExtra);
        if (!list.IsOpen)
        {
            return;
        }

        foreach (Fate fate in snapshots)
        {
            FateScore score = fateScorer.Score(fate);
            string details =
                $"Score {score:F1} · {fate.State} {fate.Progress}% · #{fate.Id.Value} · {fate.Position:f0} · r{fate.Radius}";

            ActivitySnapshotRenderer.RenderCompactWithActions(
                ui,
                navigation,
                branding.DalamudYellow,
                branding.DalamudGrey,
                fate.Name,
                details,
                fate.Position,
                $"fate_{fate.Id.Value}");

            if (southHorn
                && SouthHornEventDrops.TryGetFate(fate.Id.Value, out EventDropInfo drops))
            {
                eventDrops.Render(fate.Id.Value, drops);
            }
        }
    }

    public bool ShouldRender() => uiConfig.ShowWorldSection;
}
