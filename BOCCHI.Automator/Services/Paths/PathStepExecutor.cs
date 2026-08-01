using BOCCHI.Automator.ChainRecipes;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services.Paths;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Recipes;
using Ocelot.Extensions;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Automator.Services.Paths;

public class PathStepExecutor
(
    IChainFactory chains,
    IChainManager manager,
    IObjectTable objects,
    ICondition conditions
) : IPathStepExecutor
{
    private static readonly TimeSpan MountTimeout = TimeSpan.FromSeconds(12);

    public Task<ChainResult> Execute(IPathStep step)
    {
        IChain chain = step.PathStepData switch
        {
            Pathfind(var destination, var range) => chains.Create($"PathStep::Pathfind({destination:f2}, {range:f2})")
                .Then(_ =>
                {
                    if (ShouldSkipMount(destination))
                    {
                        return StepResult.Success();
                    }

                    if (Actions.MountRoulette.CanCast())
                    {
                        Actions.MountRoulette.Cast();
                    }

                    return StepResult.Success();
                }, "PathStep::MaybeMount")
                .WaitUntil(
                    _ => ValueTask.FromResult(IsMountedOrShouldGiveUp(destination)),
                    MountTimeout,
                    TimeSpan.FromMilliseconds(250),
                    "PathStep::WaitForMount")
                .Then<PathfindToChain, PathfinderConfig>(new(destination)
                {
                    DistanceThreshold = range > 0f ? range : 2f,
                    ShouldSnapToFloor = true
                }),

            Teleport(var id) => chains.Create($"PathStep::Teleport({id})")
                .Then<TeleportToAethernetChain, uint>(id),

            Return _ => throw new InvalidOperationException("Return path steps are handled by PathfindingHandler."),

            var _ => throw new ArgumentOutOfRangeException()
        };

        return manager.Manage(chain);
    }

    private bool ShouldSkipMount(System.Numerics.Vector3 destination)
    {
        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            return true;
        }

        if (conditions[ConditionFlag.InCombat] || conditions[ConditionFlag.Unconscious])
        {
            return true;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return true;
        }

        return player.Position.Distance(destination) <= NavigationConstants.MountMinDistance;
    }

    private bool IsMountedOrShouldGiveUp(System.Numerics.Vector3 destination)
    {
        if (conditions[ConditionFlag.Mounted])
        {
            return true;
        }

        if (ShouldSkipMount(destination))
        {
            return true;
        }

        // Still mounting / retrying.
        if (conditions[ConditionFlag.Mounting])
        {
            return false;
        }

        if (Actions.MountRoulette.CanCast())
        {
            Actions.MountRoulette.Cast();
        }

        return false;
    }
}
