using BOCCHI.Common.Data;
using BOCCHI.Treasure.Data;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.PlayerState;
using System.Numerics;

namespace BOCCHI.Treasure.Services;

public interface ICarrotTracker
{
    IReadOnlyList<Carrot> Carrots { get; }
}

public class CarrotTracker(IObjectTable objects, IPlayer player) : ICarrotTracker, IOnUpdate
{
    public IReadOnlyList<Carrot> Carrots { get; private set; } = [];

    public void Update()
    {
        if (player.PlayerCharacter == null)
        {
            Carrots = [];
            return;
        }

        Vector3 origin = player.Position;
        Carrots = objects
            .Where(o => o.ObjectKind == ObjectKind.EventObj)
            .Where(o => o.BaseId == OccultObjectType.Carrot)
            .OrderBy(o => Vector3.DistanceSquared(origin, o.Position))
            .Select(o => new Carrot(o))
            .Where(c => c.IsValid())
            .ToList();
    }
}
