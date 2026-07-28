using BOCCHI.Common.Config;
using BOCCHI.Common.Extensions;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class GatheringHandler
(
    MobFarmerConfig config,
    IMobScanner scanner,
    IObjectTable objects,
    ITargetManager targets,
    IPathfinder pathfinder
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Gathering)
{
    public override FarmerPhase? Handle()
    {
        List<IBattleNpc> inCombat = scanner.InCombat.ToList();
        List<IBattleNpc> notInCombat = scanner.NotInCombat.ToList();

        if (inCombat.Count >= config.MinimumMobsToStartFight || notInCombat.Count == 0)
        {
            pathfinder.Stop();
            return FarmerPhase.Stacking;
        }

        if (targets.Target?.IsTargetingPlayer(objects.LocalPlayer) == true)
        {
            targets.Target = null;
            pathfinder.Stop();
        }

        IBattleNpc? next = notInCombat.FirstOrDefault();
        if (next == null)
        {
            return null;
        }

        targets.Target = next;

        if (pathfinder.GetState() != PathfindingState.Idle)
        {
            return null;
        }

        if (!next.IsTargetingPlayer(objects.LocalPlayer) && !EzThrottler.Throttle("MobFarmer::Gathering::Repath"))
        {
            return null;
        }

        pathfinder.PathfindAndMoveTo(new(next.Position)
        {
            AllowFlying = false
        });

        return null;
    }
}
