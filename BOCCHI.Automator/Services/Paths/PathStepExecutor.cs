using BOCCHI.Automator.ChainRecipes;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services.Paths;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Recipes;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Automator.Services.Paths;

public class PathStepExecutor
(
    IChainFactory chains,
    IChainManager manager,
    IObjectTable objects,
    ICondition conditions,
    AutomatorConfig config
) : IPathStepExecutor
{
    public Task<ChainResult> Execute(IPathStep step)
    {
        IChain chain = step.PathStepData switch
        {
            Pathfind(var destination, var range) => BuildPathfindChain(destination, range),

            Teleport(var id) => chains.Create($"PathStep::Teleport({id})")
                .Then<TeleportToAethernetChain, uint>(id),

            Return _ => throw new InvalidOperationException("Return path steps are handled by PathfindingHandler."),

            var _ => throw new ArgumentOutOfRangeException()
        };

        return manager.Manage(chain);
    }

    private IChain BuildPathfindChain(System.Numerics.Vector3 destination, float range)
    {
        // Set when MaybeMount runs so StartGrace is not burned at compose time.
        DateTime mountStarted = DateTime.MinValue;

        return chains.Create($"PathStep::Pathfind({destination:f2}, {range:f2})")
            .Then(_ =>
            {
                mountStarted = DateTime.UtcNow;

                if (MountWait.ShouldSkip(conditions, objects, destination, config.ShouldAutoMount))
                {
                    return StepResult.Success();
                }

                MountWait.TryCast((uint)Math.Max(0, config.PreferredMountId));
                return StepResult.Success();
            }, "PathStep::MaybeMount")
            .WaitUntil(
                _ =>
                {
                    DateTime started = mountStarted == DateTime.MinValue ? DateTime.UtcNow : mountStarted;
                    return ValueTask.FromResult(
                        MountWait.IsReadyOrGiveUp(
                            conditions,
                            objects,
                            destination,
                            started,
                            config.ShouldAutoMount,
                            (uint)Math.Max(0, config.PreferredMountId)));
                },
                MountWait.Timeout,
                TimeSpan.FromMilliseconds(250),
                "PathStep::WaitForMount")
            .Then<PathfindToChain, PathfinderConfig>(new(destination)
            {
                DistanceThreshold = range > 0f ? range : 2f,
                ShouldSnapToFloor = true
            });
    }
}
