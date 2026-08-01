using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.ChainRecipes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Ocelot.Chain;
using Ocelot.Extensions;
using Ocelot.Pathfinding.Extensions;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.PlayerState;
using Ocelot.States.Score;
using System.Numerics;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using TreasureFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class FarmingPotChestsHandler
(
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
        if (memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _))
        {
            return StatePriority.Never;
        }

        return memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _) ? StatePriority.Normal : StatePriority.Never;
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
        if (!memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory farm))
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

        while(farm.Chests.Count > 0)
        {
            Vector3 target = farm.Chests.Peek();
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

        Vector3 chestPosition = farm.Chests.Peek();
        float distance = player.Position.Distance(chestPosition);

        if (distance > OpenTreasureCofferChain.InteractDistance)
        {
            if (pathfinder.IsIdle())
            {
                pathfinder.PathfindAndMoveTo(new(chestPosition));
            }

            return;
        }

        pathfinder.Stop();
        activeChain = chainManager.Manage(
            chains.Create("PotChestFarm::Open")
                .Then<OpenTreasureCofferChain, Vector3>(chestPosition)
        );
    }

    private IEnumerable<IGameObject> GetValidChests()
    {
        return objects.Where(o => o is
        {
            ObjectKind: DalamudObjectKind.Treasure,
            IsDead: false,
            IsTargetable: true
        } && o.IsValid());
    }

    private bool HasChestAt(Vector3 position)
    {
        return GetValidChests()
            .Any(o => Vector3.Distance(o.Position, position) <= ChestSearchRadius);
    }

    private bool IsChestComplete(Vector3 position)
    {
        IGameObject? chest = GetValidChests()
            .FirstOrDefault(o => Vector3.Distance(o.Position, position) <= ChestSearchRadius);

        if (chest == null)
        {
            return true;
        }

        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)chest.Address;
            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* instance = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
            return instance->Flags.HasFlag(TreasureFlags.Opened);
        }
    }
}
