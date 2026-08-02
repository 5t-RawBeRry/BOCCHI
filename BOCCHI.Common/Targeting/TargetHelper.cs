using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using Ocelot.Extensions;

namespace BOCCHI.Common.Targeting;

public static class TargetHelper
{
    public static IEnumerable<IBattleNpc> GetHostileEnemies(IObjectTable objects, Vector3 from)
    {
        return objects.OfType<IBattleNpc>()
            .Where(o => o is { IsDead: false, IsTargetable: true })
            .Where(o => o.IsHostile())
            .OrderBy(o => o.Position.Distance2D(from));
    }

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
