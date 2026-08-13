using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;

namespace BOCCHI.Common.Targeting;

public static class TargetHelper
{
    public static IBattleNpc? Closest(this IEnumerable<IBattleNpc> enemies)
    {
        return enemies.FirstOrDefault();
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

    public static IBattleNpc? Select(IEnumerable<IBattleNpc> enemies, bool preferCentroid)
    {
        List<IBattleNpc> list = enemies as List<IBattleNpc> ?? enemies.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return preferCentroid ? list.Centroid() : list.Closest();
    }
}
