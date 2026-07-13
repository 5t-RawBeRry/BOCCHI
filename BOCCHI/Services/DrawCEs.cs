using System.Numerics;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using Ocelot.Graphics;
using Ocelot.Lifecycle;
using Ocelot.Pictomancy.Services;
using Ocelot.Services.OverlayRenderer;

namespace BOCCHI.Services;

public class DrawCEs(IOverlayRenderer overlay, ICriticalEncounterRepository ces, IZoneProvider zones) : IOnRender
{
    public void Render()
    {
        foreach (var ce in ces.SnapshotWithoutForkedTower())
        {
            var radius = ce.Radius;

            overlay.StrokeCircle(ce.Position, radius, Color.Green);
            overlay.StrokeCircle(ce.Position, radius - 2f, new Color(1f, 1f, 0f));
            overlay.StrokeCircle(ce.Position, radius - 7f, Color.Red);
        }

        foreach (var crystal in zones.GetZone().GetNearbyKnowledgeCrystals())
        {
            overlay.StrokeCircle(crystal.Position, 5f, new Color(1f, 0f, 1f));
        }

        foreach (var points in GraphConfig.Lines)
        {
            for (var i = 0; i < points.Count - 1; i++)
            {
                overlay.StrokeLine(points[i], points[i + 1], new Color(1f, 0f, 0f));
            }
        }
    }
}
