using BOCCHI.Automator.ChainRecipes;
using BOCCHI.Common.Data.Paths;
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
    public Task<ChainResult> Execute(IPathStep step)
    {
        IChain chain = step.PathStepData switch
        {
            Pathfind(var destination, var range) => chains.Create($"PathStep::Pathfind({destination:f2}, {range:f2})")
                .Then(_ =>
                {
                    if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
                    {
                        return StepResult.Success();
                    }

                    if (objects.LocalPlayer is not { } player)
                    {
                        return StepResult.Failure("Didn't mount");
                    }

                    float distance = player.Position.Distance(destination);
                    if (distance > 50f)
                    {
                        Actions.MountRoulette.Cast();
                    }

                    return StepResult.Success();
                }, "PathStep::MaybeMount")
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
}
