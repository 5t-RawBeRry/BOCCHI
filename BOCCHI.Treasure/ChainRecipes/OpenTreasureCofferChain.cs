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
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.PlayerState;
using System.Numerics;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using TreasureFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags;
using TreasureState = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureState;

namespace BOCCHI.Treasure.ChainRecipes;

public class OpenTreasureCofferChain
(
    IChainFactory chains,
    IObjectTable objects,
    ITargetManager targets,
    IPlayer player,
    ICondition conditions,
    IVNavmeshIpc vnav
) : ChainRecipe<Vector3>(chains)
{
    /// <summary>Try to path this close before interacting (mesh permitting).</summary>
    public const float PathArrivalRange = 1.0f;

    /// <summary>
    /// Distance at which InteractWithObject reliably opens coffers.
    /// Matches Pandora's AutoOpenChests (2y) — 3.25y was too far and needed Pandora as a crutch.
    /// </summary>
    public const float PreferredOpenDistance = 2.0f;

    /// <summary>Still close enough to keep trying; beyond this, path in again.</summary>
    public const float MaxInteractRange = 2.75f;

    /// <summary>Legacy alias used by hunt pathing — prefer PreferredOpenDistance for gating.</summary>
    public const float InteractDistance = PreferredOpenDistance;

    public override string Name => "Open Treasure Coffer";

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

        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)chest.Address;
            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* instance =
                (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;

            if (IsOpened(instance))
            {
                return true;
            }

            // Interact already accepted — wait for Opened; don't spam InteractWithObject.
            if (instance->State is TreasureState.Opening)
            {
                return false;
            }
        }

        float dist2d = player.Position.Distance2D(chest.Position);

        // Still too far for a reliable open — close in; don't treat as failure.
        if (dist2d > MaxInteractRange)
        {
            EnsurePathing(chest.Position);
            return false;
        }

        if (dist2d > PreferredOpenDistance)
        {
            EnsurePathing(chest.Position);
            return false;
        }

        if (vnav.IsRunning())
        {
            vnav.Stop();
        }

        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)chest.Address;
            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* instance =
                (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;

            // Pandora only opens when GetIsTargetable(); same gate here.
            if (!gameObject->GetIsTargetable())
            {
                EnsurePathing(chest.Position);
                return false;
            }

            targets.Target = chest;
            // false = skip LoS — mesh stop points are often slightly occluded by the coffer model.
            TargetSystem.Instance()->InteractWithObject(gameObject, false);

            return IsOpened(instance);
        }
    }

    private void EnsurePathing(Vector3 destination)
    {
        if (!vnav.IsRunning())
        {
            vnav.PathfindAndMoveCloseTo(destination, false, PathArrivalRange);
        }
    }

    private static unsafe bool IsOpened(FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* instance)
    {
        return instance->Flags.HasFlag(TreasureFlags.Opened)
               || instance->Flags.HasFlag(TreasureFlags.FadedOut)
               || instance->State is TreasureState.Opened or TreasureState.FadingOut or TreasureState.FadedOut;
    }

    private IGameObject? GetChestAt(Vector3 position)
    {
        // Wider than max interact so slight offsets still resolve; 2D like pathing.
        const float SearchRadius = 6f;
        return objects
            .Where(o => o is { ObjectKind: DalamudObjectKind.Treasure, IsDead: false } && o.IsValid())
            .OrderBy(o => position.Distance2D(o.Position))
            .FirstOrDefault(o => position.Distance2D(o.Position) <= SearchRadius);
    }
}
