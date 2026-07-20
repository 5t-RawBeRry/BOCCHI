using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Treasure.Data;
using BOCCHI.Treasure.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Graphics;
using Ocelot.Lifecycle;
using Ocelot.Services.OverlayRenderer;
using Ocelot.Services.PlayerState;
using System.Numerics;
namespace BOCCHI.Treasure;

public class TreasureRadarDrawer
(
    ITreasureTracker tracker,
    TreasureConfig config,
    IZoneProvider zones,
    ICondition conditions,
    IOverlayRenderer overlay,
    IPlayer player
) : IOnRender
{
    public void Render()
    {
        if (!config.Enabled)
        {
            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone() || conditions[ConditionFlag.InCombat])
        {
            return;
        }

        if (config is { DrawLineToBronzeChests: false, DrawLineToSilverChests: false })
        {
            return;
        }

        if (player.PlayerCharacter == null)
        {
            return;
        }

        Vector3 origin = player.Position;

        foreach(TreasureCoffer treasure in tracker.Treasures.Where(treasure => treasure.IsValid()))
        {
            CofferType cofferType = treasure.GetCofferType();
            if (config.DrawLineToBronzeChests && cofferType == CofferType.Bronze)
            {
                overlay.StrokeLine(origin, treasure.GetPosition(), ToColor(treasure.GetColor()));
            }

            if (config.DrawLineToSilverChests && cofferType == CofferType.Silver)
            {
                overlay.StrokeLine(origin, treasure.GetPosition(), ToColor(treasure.GetColor()));
            }
        }
    }

    private static Color ToColor(Vector4 color) => new(color.X, color.Y, color.Z, color.W);
}
