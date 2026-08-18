using BOCCHI.Common.Config;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;
using System.Numerics;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class StackingHandler
(
    MobFarmerConfig config,
    IMobFarmer farmer,
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

        List<IBattleNpc> inCombat = scanner.InCombat.ToList();
        if (inCombat.Count == 0)
        {
            return FarmerPhase.Waiting;
        }

        Vector3 destination;
        if (farmer.StackPoint is { } stack)
        {
            destination = stack;
        }
        else
        {
            IBattleNpc? furthest = inCombat
                .Where(o => o.GameObjectId != targets.Target?.GameObjectId)
                .OrderBy(o => player.Position.Distance2D(o.Position))
                .LastOrDefault();

            if (furthest == null)
            {
                return FarmerPhase.Fighting;
            }

            destination = furthest.Position;
        }

        pathfinder.PathfindAndMoveTo(new(destination)
        {
            AllowFlying = false,
            ShouldSnapToFloor = true,
        });
        hasRunStack = true;

        return null;
    }
}
