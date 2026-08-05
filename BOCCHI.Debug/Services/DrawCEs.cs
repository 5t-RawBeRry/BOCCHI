using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Services;
using Ocelot.Graphics;
using Ocelot.Lifecycle;
using Ocelot.Services.OverlayRenderer;
using System.Numerics;

namespace BOCCHI.Debug.Services;

public class DrawCEs(IOverlayRenderer overlay, ICriticalEncounterRepository ces, IZoneProvider zones) : IOnRender
{
    public void Render()
    {
        foreach(CriticalEncounter ce in ces.SnapshotWithoutForkedTower())
        {
            float radius = ce.Radius;

            overlay.StrokeCircle(ce.Position, radius, Color.Green);
            overlay.StrokeCircle(ce.Position, radius - 2f, new(1f, 1f, 0f));
            overlay.StrokeCircle(ce.Position, radius - 7f, Color.Red);
        }

        foreach(KnowledgeCrystalData crystal in zones.GetZone().GetNearbyKnowledgeCrystals())
        {
            overlay.StrokeCircle(crystal.Position, 5f, new(1f, 0f, 1f));
        }

#if DEBUG
        foreach(List<Vector3> points in GraphConfig.DebugPathLines)
        {
            for(int i = 0; i < points.Count - 1; i++)
            {
                overlay.StrokeLine(points[i], points[i + 1], new(1f, 0f, 0f));
            }
        }
#endif
    }
}
