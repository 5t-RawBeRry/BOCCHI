using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Mobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Objects.Types;
using Ocelot.Graphics;
using Ocelot.Lifecycle;
using Ocelot.Services.OverlayRenderer;
using Ocelot.Services.PlayerState;
using System.Numerics;
namespace BOCCHI.MobFarmer;

public class MobFarmerDebugDrawer
(
    Func<IMobFarmer> farmerFactory,
    IMobScanner scanner,
    MobFarmerConfig config,
    IZoneProvider zones,
    IOverlayRenderer overlay,
    IPlayer player
) : IOnRender
{
    private IMobFarmer? farmer;

    private IMobFarmer Farmer => farmer ??= farmerFactory();

    public void Render()
    {
        if (!config.RenderDebugLines)
        {
            return;
        }

        if (!Farmer.Running && !config.RenderDebugLinesWhileNotRunning)
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

        Vector3 origin = player.Position;

        foreach(IBattleNpc mob in scanner.NotInCombat)
        {
            Color color = MobData.IsSelected(mob.NameId, config.Mobs)
                ? new(0.9f, 0.1f, 0.9f)
                : new Color(0.9f, 0.1f, 0.1f);
            overlay.StrokeLine(origin, mob.Position, color);
        }

        foreach(IBattleNpc mob in scanner.InCombat)
        {
            overlay.StrokeLine(origin, mob.Position, new(0.1f, 0.9f, 0.1f));
        }
    }
}
