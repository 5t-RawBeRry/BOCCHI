#if DEBUG
using BOCCHI.Debug;
#endif
using BOCCHI.Common.Data.Zones;
using Ocelot.Lifecycle;
using Ocelot.Windows;

namespace BOCCHI.Services;

public class OpenWindows(
    IMainWindow? main,
    IConfigWindow? config,
#if DEBUG
    IDebugWindow? debug,
#endif
    IZoneProvider zones
) : IOnStart
{
    public void OnStart()
    {
        main?.IsOpen = true;
        config?.IsOpen = true;
#if DEBUG
        debug?.IsOpen = true;
#endif

        var zone = zones.GetZone();
        zone.GetGraph();
    }
}
