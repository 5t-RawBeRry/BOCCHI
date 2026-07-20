using BOCCHI.Common.Config;
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
        if (inCombat.Count > 0 && EzThrottler.Throttle("MobFarmer::Fighting::Target", 250))
        {
            targets.Target = inCombat.OrderBy(o => player.Position.Distance2D(o.Position)).First();
        }

        bool anyInCombat = inCombat.Count > 0;
        bool shouldReturnHome = config.ReturnToStartInWaitingPhase
                                && player.Position.Distance2D(farmer.StartingPoint) >= config.MinEuclideanDistanceToReturnHome;

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
