using BOCCHI.Common.Config;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using BOCCHI.MobFarmer.Services;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class StackingHandler(
    MobFarmerConfig config,
    IMobScanner scanner,
    ITargetManager targets,
    IPathfinder pathfinder,
    IPlayer player,
    IRotationPlugin rotation
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
        var pathState = pathfinder.GetState();

        if (hasRunStack)
        {
            if (pathState is PathfindingState.Moving or PathfindingState.Pathfinding)
            {
                if (TimeInState >= TimeSpan.FromSeconds(config.StackingTimeoutSeconds))
                {
                    pathfinder.Stop();
                    rotation.PhantomJobOn();
                    return FarmerPhase.Fighting;
                }

                return null;
            }

            hasRunStack = false;
            rotation.PhantomJobOn();
            return FarmerPhase.Fighting;
        }

        var furthest = scanner.InCombat
            .Where(o => o.GameObjectId != targets.Target?.GameObjectId)
            .OrderBy(o => player.Position.Distance2D(o.Position))
            .LastOrDefault();

        if (furthest == null)
        {
            rotation.PhantomJobOn();
            return FarmerPhase.Fighting;
        }

        pathfinder.PathfindAndMoveTo(new PathfinderConfig(furthest.Position)
        {
            AllowFlying = false,
        });
        hasRunStack = true;

        return null;
    }
}
