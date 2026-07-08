using BOCCHI.Common.Config;
using BOCCHI.Common.Extensions;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class GatheringHandler(
    MobFarmerConfig config,
    IMobScanner scanner,
    IObjectTable objects,
    ITargetManager targets,
    IPathfinder pathfinder
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Gathering)
{
    public override FarmerPhase? Handle()
    {
        var inCombat = scanner.InCombat.ToList();
        var notInCombat = scanner.NotInCombat.ToList();

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

        var next = notInCombat.FirstOrDefault();
        if (next == null)
        {
            return null;
        }

        targets.Target = next;

        if (pathfinder.GetState() != PathfindingState.Idle)
        {
            return null;
        }

        if (!next.IsTargetingPlayer(objects.LocalPlayer) && !EzThrottler.Throttle("MobFarmer::Gathering::Repath", 500))
        {
            return null;
        }

        pathfinder.PathfindAndMoveTo(new PathfinderConfig(next.Position)
        {
            AllowFlying = false,
        });

        return null;
    }
}
