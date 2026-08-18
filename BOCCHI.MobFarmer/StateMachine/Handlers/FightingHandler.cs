using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Targeting;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class FightingHandler
(
    MobFarmerConfig config,
    MovementConfig movementConfig,
    IMobFarmer farmer,
    IMobScanner scanner,
    ITargetManager targets,
    ICondition conditions,
    IObjectTable objects,
    IPathfinder pathfinder,
    IZoneProvider zones,
    IPlayer player
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Fighting)
{
    public override FarmerPhase? Handle()
    {
        List<IBattleNpc> inCombat = scanner.InCombat.ToList();
        if (config.ShouldHandleTargeting
            && inCombat.Count > 0
            && EzThrottler.Throttle("MobFarmer::Fighting::Target", 250))
        {
            IBattleNpc? target = TargetHelper.Select(inCombat, player.Position, config.ForceTargetCentralEnemy);
            if (target != null)
            {
                targets.Target = target;
            }
        }

        bool anyInCombat = inCombat.Count > 0;
        bool shouldReturnHome = config.ReturnToStartInWaitingPhase
                                && player.Position.Distance2D(farmer.StartingPoint) >= config.MinEuclideanDistanceToReturnHome;

        // Finish the fight before gathering again — do not top up mid-pack.
        if (shouldReturnHome && !anyInCombat)
        {
            if (pathfinder.GetState() == PathfindingState.Idle)
            {
                pathfinder.PathfindAndMoveTo(new(farmer.StartingPoint)
                {
                    AllowFlying = false
                });
            }

            MountWait.TryCastIfNeeded(
                conditions,
                objects,
                farmer.StartingPoint,
                movementConfig.ShouldAutoMount,
                movementConfig.PreferredMountId,
                zones.GetZone().IsInBasecamp());

            return player.Position.Distance2D(farmer.StartingPoint) <= 2f ? FarmerPhase.Waiting : null;
        }

        if (!anyInCombat && !conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
            return FarmerPhase.Waiting;
        }

        return null;
    }
}
