using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class WaitingHandler
(
    MobFarmerConfig config,
    MovementConfig movementConfig,
    IMobFarmer farmer,
    IMobScanner scanner,
    ICondition conditions,
    IObjectTable objects,
    IPathfinder pathfinder,
    IZoneProvider zones,
    IPlayer player
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Waiting)
{
    private const float ArriveRange = 8f;

    public override FarmerPhase? Handle()
    {
        if (scanner.InCombat.Any())
        {
            return FarmerPhase.Fighting;
        }

        if (config.OnlyStartOutOfCombat && conditions[ConditionFlag.InCombat])
        {
            return null;
        }

        if (conditions[ConditionFlag.InCombat])
        {
            return FarmerPhase.Fighting;
        }

        float homeDistance = player.Position.Distance2D(farmer.StartingPoint);
        if (farmer.NeedsApproachSpot)
        {
            if (homeDistance <= ArriveRange)
            {
                farmer.MarkArrivedAtSpot();
            }
            else
            {
                if (pathfinder.GetState() == PathfindingState.Idle)
                {
                    pathfinder.PathfindAndMoveTo(new(farmer.StartingPoint)
                    {
                        AllowFlying = false,
                        DistanceThreshold = 2f,
                    });
                }

                MountWait.TryCastIfNeeded(
                    conditions,
                    objects,
                    farmer.StartingPoint,
                    movementConfig.ShouldAutoMount,
                    movementConfig.PreferredMountId,
                    zones.GetZone().IsInBasecamp());

                return null;
            }
        }

        int free = MobFarmerPack.CountTowardMinimum(scanner.NotInCombat, config.CountSpecialMobsTowardMinimum);
        if (free == 0)
        {
            return null;
        }

        return free >= config.MinimumMobsToStartLoop ? FarmerPhase.Buffing : null;
    }
}
