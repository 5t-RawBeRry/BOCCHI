using BOCCHI.Buff.Data;
using BOCCHI.Common.Data.KnowledgeCrystals;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;
using System.Numerics;

namespace BOCCHI.Buff.StateMachine.Handlers;

public class ApproachingKnowledgeCrystalHandler
(
    IZoneProvider zones,
    IPlayer player,
    IPathfinder pathfinder,
    ICondition conditions
) : FlowStateHandler<BuffState>(BuffState.ApproachingKnowledgeCrystal)
{
    private const float InteractionRange = 5f;

    public override BuffState? Handle()
    {
        IZone zone = zones.GetZone();
        if (!zone.IsOccultCrescentZone())
        {
            return null;
        }

        List<KnowledgeCrystalData> crystals = zone.GetNearbyKnowledgeCrystals().ToList();
        if (crystals.Count == 0)
        {
            return BuffState.NoCrystalsFound;
        }

        KnowledgeCrystalData closest = crystals[0];
        float distance = player.Position.Distance2D(closest.Position);
        if (distance <= InteractionRange)
        {
            pathfinder.Stop();

            if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
            {
                if (!conditions[ConditionFlag.Mounting])
                {
                    Actions.Dismount.Cast();
                }

                return null;
            }

            return BuffState.ChoosingBuffToApply;
        }

        if (pathfinder.GetState() != PathfindingState.Idle)
        {
            return null;
        }

        Vector3 destination = closest.Position.GetApproachPosition(player.Position, InteractionRange - 0.2f);
        pathfinder.PathfindAndMoveTo(new(destination)
        {
            DistanceThreshold = 1.5f,
            ShouldSnapToFloor = true
        });

        return null;
    }
}
