using BOCCHI.Common.Config;
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
    CombatConfig combat,
    IMobFarmer farmer,
    IMobScanner scanner,
    ITargetManager targets,
    ICondition conditions,
    IPathfinder pathfinder,
    IPlayer player
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Fighting)
{
    public override FarmerPhase? Handle()
    {
        List<IBattleNpc> inCombat = scanner.InCombat.ToList();
        if (combat.ShouldHandleTargeting
            && inCombat.Count > 0
            && EzThrottler.Throttle("MobFarmer::Fighting::Target", 250))
        {
            IBattleNpc? target = TargetHelper.Select(inCombat, combat.ForceTargetCentralEnemy);
            if (target != null)
            {
                targets.Target = target;
            }
        }

        bool anyInCombat = inCombat.Count > 0;
        bool shouldReturnHome = config.ReturnToStartInWaitingPhase
                                && player.Position.Distance2D(farmer.StartingPoint) >= config.MinEuclideanDistanceToReturnHome;

        // Keep pulling until the configured pack size if free mobs remain.
        if (anyInCombat
            && inCombat.Count < config.MinimumMobsToStartFight
            && scanner.NotInCombat.Any())
        {
            return FarmerPhase.Gathering;
        }

        if (shouldReturnHome && !anyInCombat)
        {
            if (pathfinder.GetState() == PathfindingState.Idle)
            {
                pathfinder.PathfindAndMoveTo(new(farmer.StartingPoint)
                {
                    AllowFlying = false
                });
            }

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
