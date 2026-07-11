using BOCCHI.Common;
using BOCCHI.Common.Data.Zones;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Graphics;
using Ocelot.Services.UI;
using Ocelot.Services.WindowManager;

namespace BOCCHI.Renderers;

public class MainRenderer(IServiceProvider services, IZoneProvider zones, IUIService ui) : IMainRenderer
{
    private IEnumerable<IDynamicRenderer>? renderers;

    private IEnumerable<IDynamicRenderer> OrderedRenderers =>
        (renderers ??= services.GetServices<IDynamicRenderer>())
        .Where(r => r.ShouldRender())
        .OrderBy(r => r.Order);

    public void Render()
    {
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            // TODO: Translate
            ui.Text("Not in a supported Occult Crescent Zone.", Color.Red);
            return;
        }

        foreach (var renderer in OrderedRenderers)
        {
            renderer.Render();
        }
    }
}
