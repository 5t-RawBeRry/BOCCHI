using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Mobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.MobFarmer.Services;
using Ocelot.Graphics;
using Ocelot.Lifecycle;
using Ocelot.Services.OverlayRenderer;
using Ocelot.Services.PlayerState;

namespace BOCCHI.MobFarmer;

public class MobFarmerDebugDrawer(
    IMobFarmer farmer,
    IMobScanner scanner,
    MobFarmerConfig config,
    IZoneProvider zones,
    IOverlayRenderer overlay,
    IPlayer player
) : IOnRender
{
    public void Render()
    {
        if (!config.RenderDebugLines)
        {
            return;
        }

        if (!farmer.Running && !config.RenderDebugLinesWhileNotRunning)
        {
            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        if (player.PlayerCharacter == null)
        {
            return;
        }

        var origin = player.Position;

        foreach (var mob in scanner.NotInCombat)
        {
            var color = MobData.IsSelected(mob.NameId, config.Mobs)
                ? new Color(0.9f, 0.1f, 0.9f)
                : new Color(0.9f, 0.1f, 0.1f);
            overlay.StrokeLine(origin, mob.Position, color);
        }

        foreach (var mob in scanner.InCombat)
        {
            overlay.StrokeLine(origin, mob.Position, new Color(0.1f, 0.9f, 0.1f));
        }
    }
}
