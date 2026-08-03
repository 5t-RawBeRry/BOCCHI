using BOCCHI.Common.Config;
using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Traps;
using BOCCHI.Common.Data.Traps.SouthHorn;
using BOCCHI.Common.Data.Zones;
using Ocelot.Graphics;
using Ocelot.Lifecycle;
using Ocelot.Services.OverlayRenderer;
using Ocelot.Services.PlayerState;
using System.Numerics;

namespace BOCCHI.World;

/// <summary>
///     Authored trap spawn highlights inside South Horn Forked Tower (#116).
/// </summary>
public class ForkedTowerTrapDrawer
(
    ForkedTowerConfig config,
    IZoneProvider zones,
    IOverlayRenderer overlay,
    IPlayer player
) : IOnRender
{
    private static readonly Color TrapColor = new(1f, 0.85f, 0.2f, 0.9f);

    private static readonly Color BigTrapColor = new(1f, 0.35f, 0.15f, 0.9f);

    public void Render()
    {
        if (!config.Enabled || !config.DrawPotentialTrapPositions)
        {
            return;
        }

        IZone zone = zones.GetZone();
        if (zone.ZoneId != ZoneId.SouthHorn || !zone.IsInForkedTower())
        {
            return;
        }

        if (player.PlayerCharacter == null)
        {
            return;
        }

        Vector3 origin = player.Position;
        float range = config.TrapDrawRange;

        foreach (TrapGroup group in SouthHornTrapData.Groups)
        {
            if (group.GetDistance2D(origin) > range)
            {
                continue;
            }

            foreach (TrapDatum trap in group.Traps)
            {
                Color color = trap.Type == OccultObjectType.BigTrap ? BigTrapColor : TrapColor;
                overlay.StrokeCircle(trap.Position, 4f, color);
            }
        }
    }
}
