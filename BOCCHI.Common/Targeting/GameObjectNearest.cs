using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Ocelot.Extensions;

namespace BOCCHI.Common.Targeting;

public static class GameObjectNearest
{
    public static IGameObject? Find2D(
        IEnumerable<IGameObject> objects,
        Vector3 origin,
        float radius,
        Func<IGameObject, bool>? predicate = null)
    {
        IGameObject? best = null;
        float bestDist = float.MaxValue;
        foreach (IGameObject obj in objects)
        {
            if (predicate != null && !predicate(obj))
            {
                continue;
            }

            float dist = origin.Distance2D(obj.Position);
            if (dist > radius || dist >= bestDist)
            {
                continue;
            }

            best = obj;
            bestDist = dist;
        }

        return best;
    }
}
