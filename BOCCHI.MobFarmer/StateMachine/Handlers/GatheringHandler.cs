using BOCCHI.Common.Config;
using BOCCHI.Common.Extensions;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Actions;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class GatheringHandler
(
    MobFarmerConfig config,
    IMobScanner scanner,
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

        if (inCombat.Count >= config.MinimumMobsToStartFight)
        {
            pathfinder.Stop();
            return FarmerPhase.Stacking;
        }

        // Contested packs (all mobs have a target that isn't us) leave both lists empty —
        // do not spin Stacking → Fighting → Waiting with nothing to fight.
        if (notInCombat.Count == 0)
        {
            pathfinder.Stop();
            return inCombat.Count > 0 ? FarmerPhase.Stacking : FarmerPhase.Waiting;
        }

        if (targets.Target?.IsTargetingPlayer(objects.LocalPlayer) == true)
        {
            targets.Target = null;
            pathfinder.Stop();
        }

        IBattleNpc? next = notInCombat
            .OrderBy(o => player.Position.Distance2D(o.Position))
            .FirstOrDefault();
        if (next == null)
        {
            return null;
        }

        if (conditions[ConditionFlag.Mounted] && Actions.Dismount.CanCast())
        {
            Actions.Dismount.Cast();
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
            AllowFlying = false,
            DistanceThreshold = 2f,
        });

        return null;
    }
}
