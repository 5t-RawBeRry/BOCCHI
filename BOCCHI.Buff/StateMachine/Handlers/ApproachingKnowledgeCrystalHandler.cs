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
    private const float CrystalInteractionRange = 5f;

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

        BuffZone? buffZone = zone.GetBuffZone();
        KnowledgeCrystalData closest = crystals[0];

        // Prefer the fixed buff annulus when authored; fall back to crystal approach.
        bool inRange = buffZone is { } zoneData
            ? zoneData.Contains2D(player.Position)
            : player.Position.Distance2D(closest.Position) <= CrystalInteractionRange;

        if (inRange)
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

        Vector3 destination = buffZone is { } bz
            ? bz.GetApproachPoint(player.Position)
            : closest.Position.GetApproachPosition(player.Position, CrystalInteractionRange - 0.2f);

        pathfinder.PathfindAndMoveTo(new(destination)
        {
            DistanceThreshold = 1.0f,
            ShouldSnapToFloor = true
        });

        return null;
    }
}
