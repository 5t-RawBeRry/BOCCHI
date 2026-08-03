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
    public const float PathArrivalRange = 1.0f;

    public const float PreferredOpenDistance = 2.0f;

    public const float MaxInteractRange = 2.75f;

    public const float InteractDistance = PreferredOpenDistance;

    public override string Name => "Open Treasure Coffer";

    protected override IChain Compose(IChain chain, Vector3 targetPosition)
    {
        var pathState = new PathState();

        return chain
            .UseMiddleware<LogChainMiddleware>()
            .UseStepMiddleware<LogStepMiddleware>()
            .UseStepMiddleware<RunOnMainThreadMiddleware>()
            .WaitUntil(
                _ => ValueTask.FromResult(TryInteract(targetPosition, pathState)),
                TimeSpan.FromSeconds(45),
                TimeSpan.FromMilliseconds(250),
                "OpenTreasureCofferChain::Interact"
            );
    }

    private bool TryInteract(Vector3 targetPosition, PathState pathState)
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

            if (instance->State is TreasureState.Opening)
            {
                return false;
            }
        }

        float dist2d = player.Position.Distance2D(chest.Position);

        if (dist2d > PreferredOpenDistance)
        {
            EnsurePathing(chest.Position, pathState);
            return false;
        }

        if (vnav.IsRunning())
        {
            vnav.Stop();
        }

        pathState.LastTarget = null;

        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)chest.Address;
            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* instance =
                (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;

            if (!gameObject->GetIsTargetable())
            {
                EnsurePathing(chest.Position, pathState);
                return false;
            }

            targets.Target = chest;
            TargetSystem.Instance()->InteractWithObject(gameObject, false);

            return IsOpened(instance);
        }
    }

    private void EnsurePathing(Vector3 destination, PathState pathState)
    {
        const float RepathDrift = 1.5f;
        bool drifted = pathState.LastTarget is not { } last
                       || Vector3.DistanceSquared(last, destination) > RepathDrift * RepathDrift;

        if (!vnav.IsRunning() || drifted)
        {
            pathState.LastTarget = destination;
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
        const float SearchRadius = 6f;
        return objects
            .Where(o => o is { ObjectKind: DalamudObjectKind.Treasure, IsDead: false } && o.IsValid())
            .OrderBy(o => position.Distance2D(o.Position))
            .FirstOrDefault(o => position.Distance2D(o.Position) <= SearchRadius);
    }

    private sealed class PathState
    {
        public Vector3? LastTarget;
    }
}
