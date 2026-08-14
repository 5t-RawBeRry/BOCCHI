using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Ocelot.Extensions;

namespace BOCCHI.Common.Targeting;

public static class TargetHelper
{
    public static IBattleNpc? Closest(this IEnumerable<IBattleNpc> enemies, Vector3 from)
    {
        IBattleNpc? best = null;
        float bestDist = float.MaxValue;
        foreach (IBattleNpc npc in enemies)
        {
            float dist = from.Distance2D(npc.Position);
            if (dist >= bestDist)
            {
                continue;
            }

            best = npc;
            bestDist = dist;
        }

        return best;
    }

    public static IBattleNpc? Centroid(this IEnumerable<IBattleNpc> enemies)
    {
        List<IBattleNpc> list = enemies as List<IBattleNpc> ?? enemies.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        Vector3 sum = Vector3.Zero;
        foreach (IBattleNpc npc in list)
        {
            sum += npc.Position;
        }

        Vector3 centroid = sum / list.Count;

        return list
            .OrderBy(npc => Vector3.DistanceSquared(npc.Position, centroid))
            .FirstOrDefault();
    }

    public static IBattleNpc? Select(IEnumerable<IBattleNpc> enemies, Vector3 from, bool preferCentroid)
    {
        List<IBattleNpc> list = enemies as List<IBattleNpc> ?? enemies.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return preferCentroid ? list.Centroid() : list.Closest(from);
    }
}
