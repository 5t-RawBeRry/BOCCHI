using BOCCHI.Common;
using BOCCHI.Common.Data.Zones;
using BOCCHI.UI;
using Dalamud.Bindings.ImGui;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Graphics;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Services.WindowManager;
using Ocelot.Windows;

namespace BOCCHI.Renderers;

public class MainRenderer
(
    IServiceProvider services,
    IZoneProvider zones,
    IUIService ui,
    OperationalStatusBar statusBar,
    ITranslator<MainWindow> translator
) : IMainRenderer
{
    private IEnumerable<IDynamicRenderer>? renderers;

    private readonly HashSet<MainWindowSection> openedWhileActive = [];

    private IEnumerable<IDynamicRenderer> OrderedRenderers =>
        (renderers ??= services.GetServices<IDynamicRenderer>())
        .Where(r => r.ShouldRender())
        .OrderBy(r => r.Section)
        .ThenBy(r => r.Order);

    public void Render()
    {
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            ui.Text(translator.T(".unsupported_zone"), Color.Red);
            return;
        }

        statusBar.Render();
        ImGui.Spacing();

        foreach(MainWindowSection section in Enum.GetValues<MainWindowSection>())
        {
            List<IDynamicRenderer> sectionRenderers = OrderedRenderers.Where(r => r.Section == section).ToList();
            if (sectionRenderers.Count == 0)
            {
                continue;
            }

            // Trackers: always visible, no collapsing header.
            if (section == MainWindowSection.Trackers)
            {
                ui.Text(GetSectionTitle(section));
                ImGui.Indent();
                foreach (IDynamicRenderer renderer in sectionRenderers)
                {
                    if (renderer.SubsectionTitle is { } title)
                    {
                        ui.Text(title);
                        ImGui.Indent();
                    }

                    renderer.Render();

                    if (renderer.SubsectionTitle != null)
                    {
                        ImGui.Unindent();
                    }

                    ImGui.Spacing();
                }

                ImGui.Unindent();
                continue;
            }

            bool forceOpen = section switch
            {
                MainWindowSection.Automation => statusBar.IllegalModeActive,
                MainWindowSection.PotsTreasure => statusBar.PotsTreasureActive,
                MainWindowSection.MobFarmer => statusBar.MobFarmerActive,
                MainWindowSection.Treasure => statusBar.StandaloneTreasureHuntActive,
                var _ => false
            };

            // Open once when a mode becomes active (do not force every frame — that fights collapse / layout).
            if (forceOpen)
            {
                if (openedWhileActive.Add(section))
                {
                    ImGui.SetNextItemOpen(true);
                }
            }
            else
            {
                openedWhileActive.Remove(section);
                ImGui.SetNextItemOpen(false, ImGuiCond.FirstUseEver);
            }

            if (!ImGui.CollapsingHeader(GetSectionTitle(section)))
            {
                continue;
            }

            ImGui.Indent();
            ImGui.Spacing();

            foreach(IDynamicRenderer renderer in sectionRenderers)
            {
                if (renderer.SubsectionTitle is { } title)
                {
                    ui.Text(title);
                    ImGui.Indent();
                }

                renderer.Render();

                if (renderer.SubsectionTitle != null)
                {
                    ImGui.Unindent();
                }

                ImGui.Spacing();
            }

            ImGui.Unindent();
        }
    }

    private string GetSectionTitle(MainWindowSection section) => translator.T($".sections.{section.ToString().ToLowerInvariant()}");
}
