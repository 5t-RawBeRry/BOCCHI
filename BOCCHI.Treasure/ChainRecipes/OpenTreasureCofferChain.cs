using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Ocelot.Actions;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Middleware.Chain;
using Ocelot.Chain.Middleware.Step;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;
using System.Numerics;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using TreasureFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags;

namespace BOCCHI.Treasure.ChainRecipes;

public class OpenTreasureCofferChain
(
    IChainFactory chains,
    IObjectTable objects,
    ITargetManager targets,
    IPlayer player,
    ICondition conditions
) : ChainRecipe<Vector3>(chains)
{
    public const float InteractDistance = 2f;

    public override string Name
    {
        get => "Open Treasure Coffer";
    }

    protected override IChain Compose(IChain chain, Vector3 targetPosition)
    {
        return chain
            .UseMiddleware<LogChainMiddleware>()
            .UseStepMiddleware<LogStepMiddleware>()
            .UseStepMiddleware<RunOnMainThreadMiddleware>()
            .WaitUntil(
                _ => ValueTask.FromResult(TryInteract(targetPosition)),
                TimeSpan.FromSeconds(45),
                TimeSpan.FromMilliseconds(250),
                "OpenTreasureCofferChain::Interact"
            );
    }

    private bool TryInteract(Vector3 targetPosition)
    {
        if (!EzThrottler.Throttle("ChestInteract", 250))
        {
            return false;
        }

        if (conditions[ConditionFlag.Mounted] || conditions[ConditionFlag.Mounting])
        {
            if (Actions.Dismount.CanCast())
            {
                Actions.Dismount.Cast();
            }

            return false;
        }

        IGameObject? chest = GetChestAt(targetPosition);
        if (chest == null)
        {
            // Keep waiting — missing object is not success (spawn lag / offset).
            return false;
        }

        if (player.Position.Distance(chest.Position) > InteractDistance)
        {
            return false;
        }

        unsafe
        {
            targets.Target = chest;
            GameObject* gameObject = (GameObject*)(void*)chest.Address;
            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* instance = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
            TargetSystem.Instance()->InteractWithObject(gameObject);

            if (instance->Flags.HasFlag(TreasureFlags.Opened))
            {
                return true;
            }
        }

        return false;
    }

    private IGameObject? GetChestAt(Vector3 position)
    {
        // Search a bit wider than interact range so slight layout offsets still resolve.
        const float SearchRadius = 5f;
        return objects
            .Where(o => o is { ObjectKind: DalamudObjectKind.Treasure, IsDead: false, IsTargetable: true } && o.IsValid())
            .FirstOrDefault(o => Vector3.Distance(o.Position, position) <= SearchRadius);
    }
}
