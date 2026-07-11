using System.Numerics;
using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.ChainRecipes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;
using TreasureFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class FarmingPotChestsHandler(
    IAutomatorMemory memory,
    IChainFactory chains,
    IChainManager chainManager,
    IPathfinder pathfinder,
    IObjectTable objects,
    ICondition conditions,
    IPlayer player
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.FarmingPotChests)
{
    private const float ChestSearchRadius = 5f;

    private Task<ChainResult>? activeChain;

    public override StatePriority GetScore()
    {
        if (memory.TryRemember<GoalPathStepMemory>(out _))
        {
            return StatePriority.Never;
        }

        return memory.TryRemember<PotChestFarmMemory>(out _) ? StatePriority.Normal : StatePriority.Never;
    }

    public override void Enter()
    {
        base.Enter();
        chainManager.CancelAll();
        pathfinder.Stop();
        activeChain = null;
    }

    public override void Exit(AutomatorState next)
    {
        base.Exit(next);
        chainManager.CancelAll();
        pathfinder.Stop();
        activeChain = null;
    }

    public override void Handle()
    {
        if (!memory.TryRemember<PotChestFarmMemory>(out var farm))
        {
            return;
        }

        if (activeChain is { IsCompleted: false })
        {
            return;
        }

        activeChain = null;

        if (conditions[ConditionFlag.InCombat])
        {
            pathfinder.Stop();
            return;
        }

        while (farm.Chests.Count > 0)
        {
            var target = farm.Chests.Peek();
            if (!HasChestAt(target) || IsChestComplete(target))
            {
                farm.Chests.Dequeue();
                continue;
            }

            break;
        }

        if (farm.Chests.Count == 0)
        {
            memory.Forget<PotChestFarmMemory>();
            return;
        }

        var chestPosition = farm.Chests.Peek();
        var distance = player.Position.Distance(chestPosition);

        if (distance > OpenTreasureCofferChain.InteractDistance)
        {
            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new PathfinderConfig(chestPosition));
            }

            return;
        }

        pathfinder.Stop();
        activeChain = chainManager.Manage(
            chains.Create("PotChestFarm::Open")
                .Then<OpenTreasureCofferChain, Vector3>(chestPosition)
        );
    }

    public override void Render()
    {
        base.Render();

        if (!memory.TryRemember<PotChestFarmMemory>(out var farm))
        {
            return;
        }

        // Rendered by AutomatorRenderer via memory inspection
    }

    private IEnumerable<IGameObject> GetValidChests()
    {
        return objects.Where(o => o is
        {
            ObjectKind: DalamudObjectKind.Treasure,
            IsDead: false,
            IsTargetable: true,
        } && o.IsValid());
    }

    private bool HasChestAt(Vector3 position)
    {
        return GetValidChests()
            .Any(o => Vector3.Distance(o.Position, position) <= ChestSearchRadius);
    }

    private bool IsChestComplete(Vector3 position)
    {
        var chest = GetValidChests()
            .FirstOrDefault(o => Vector3.Distance(o.Position, position) <= ChestSearchRadius);

        if (chest == null)
        {
            return true;
        }

        unsafe
        {
            var gameObject = (GameObject*)(void*)chest.Address;
            var instance = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
            return instance->Flags.HasFlag(TreasureFlags.Opened);
        }
    }
}
