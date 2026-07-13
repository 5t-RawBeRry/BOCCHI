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

public class MainRenderer(
    IServiceProvider services,
    IZoneProvider zones,
    IUIService ui,
    OperationalStatusBar statusBar,
    ITranslator<MainWindow> translator
) : IMainRenderer
{
    private IEnumerable<IDynamicRenderer>? renderers;

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

        foreach (var section in Enum.GetValues<MainWindowSection>())
        {
            var sectionRenderers = OrderedRenderers.Where(r => r.Section == section).ToList();
            if (sectionRenderers.Count == 0)
            {
                continue;
            }

            var defaultOpen = section switch
            {
                MainWindowSection.Automation => statusBar.AnyAutomationActive,
                MainWindowSection.World => false,
                _ => true,
            };

            ImGui.SetNextItemOpen(defaultOpen, ImGuiCond.FirstUseEver);

            if (!ImGui.CollapsingHeader(GetSectionTitle(section)))
            {
                continue;
            }

            ImGui.Indent();

            foreach (var renderer in sectionRenderers)
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

    private string GetSectionTitle(MainWindowSection section)
    {
        return translator.T($".sections.{section.ToString().ToLowerInvariant()}");
    }
}
