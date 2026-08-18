using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Extensions;
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
using System.Numerics;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class GatheringHandler
(
    MobFarmerConfig config,
    IMobFarmer farmer,
    IMobScanner scanner,
    FarmerPullAssist pull,
    IObjectTable objects,
    ITargetManager targets,
    IPathfinder pathfinder,
    ICondition conditions,
    IPlayer player
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Gathering)
{
    public override FarmerPhase? Handle()
    {
        List<IBattleNpc> inCombat = scanner.InCombat.ToList();
        List<IBattleNpc> notInCombat = scanner.NotInCombat.ToList();

        if (MobFarmerPack.CountTowardMinimum(inCombat, config.CountSpecialMobsTowardMinimum)
            >= farmer.EffectiveMinimumMobsToStartFight)
        {
            pathfinder.Stop();
            return FarmerPhase.Stacking;
        }

        if (notInCombat.Count == 0)
        {
            pathfinder.Stop();
            return inCombat.Count > 0 ? FarmerPhase.Stacking : FarmerPhase.Waiting;
        }

        if (config.ShouldHandleTargeting
            && targets.Target?.IsTargetingPlayer(objects.LocalPlayer) == true)
        {
            targets.Target = null;
            pathfinder.Stop();
        }

        List<IBattleNpc> ordered = notInCombat
            .OrderBy(o => player.Position.Distance2D(o.Position))
            .ToList();
        IBattleNpc current = ordered[0];
        Vector3? nextPos = ordered.Count > 1
            ? ordered[1].Position
            : farmer.StackPoint;

        if (DismountAssist.TryDismount(conditions))
        {
            return null;
        }

        if (config.ShouldHandleTargeting)
        {
            targets.Target = current;
        }

        float dist = player.Position.Distance2D(current.Position);
        if (dist <= FarmerPullAssist.PullRange)
        {
            pull.TryPull(current);
        }

        if (pathfinder.GetState() != PathfindingState.Idle)
        {
            return null;
        }

        if (!current.IsTargetingPlayer(objects.LocalPlayer)
            && !EzThrottler.Throttle("MobFarmer::Gathering::Repath"))
        {
            return null;
        }

        Vector3 destination = Destination(current.Position, nextPos, dist);
        pathfinder.PathfindAndMoveTo(new(destination)
        {
            AllowFlying = false,
            DistanceThreshold = 2f,
            ShouldSnapToFloor = true,
        });

        return null;
    }

    private static Vector3 Destination(Vector3 current, Vector3? next, float distToCurrent)
    {
        if (distToCurrent <= FarmerPullAssist.PullRange)
        {
            return next ?? current;
        }

        if (next is not { } nextPos)
        {
            return current;
        }

        Vector3 toNext = nextPos - current;
        toNext.Y = 0;
        if (toNext.LengthSquared() < 0.01f)
        {
            return current;
        }

        Vector3 dir = Vector3.Normalize(toNext);
        return current + (dir * (FarmerPullAssist.PullRange * 0.7f));
    }
}
