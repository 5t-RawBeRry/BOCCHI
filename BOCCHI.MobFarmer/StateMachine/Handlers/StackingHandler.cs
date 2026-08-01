using BOCCHI.Common.Config;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class StackingHandler
(
    MobFarmerConfig config,
    IMobScanner scanner,
    ITargetManager targets,
    IPathfinder pathfinder,
    IPlayer player
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Stacking)
{
    private bool hasRunStack;

    public override void Enter()
    {
        base.Enter();
        hasRunStack = false;
    }

    public override FarmerPhase? Handle()
    {
        PathfindingState pathState = pathfinder.GetState();

        if (hasRunStack)
        {
            if (pathState is PathfindingState.Moving or PathfindingState.Pathfinding)
            {
                if (TimeInState >= TimeSpan.FromSeconds(config.StackingTimeoutSeconds))
                {
                    pathfinder.Stop();
                    return FarmerPhase.Fighting;
                }

                return null;
            }

            hasRunStack = false;
            return FarmerPhase.Fighting;
        }

        IBattleNpc? furthest = scanner.InCombat
            .Where(o => o.GameObjectId != targets.Target?.GameObjectId)
            .OrderBy(o => player.Position.Distance2D(o.Position))
            .LastOrDefault();

        if (furthest == null)
        {
            return FarmerPhase.Fighting;
        }

        pathfinder.PathfindAndMoveTo(new(furthest.Position)
        {
            AllowFlying = false
        });
        hasRunStack = true;

        return null;
    }
}
