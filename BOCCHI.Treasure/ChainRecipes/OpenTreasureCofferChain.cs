using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Ocelot.Chain;
using Ocelot.Chain.Extensions;
using Ocelot.Chain.Middleware.Chain;
using Ocelot.Chain.Middleware.Step;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.PlayerState;
using System.Numerics;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using TreasureFlags = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags;
using TreasureState = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureState;
using CsObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;

namespace BOCCHI.Treasure.ChainRecipes;

/// <summary>Where to open a coffer; optional BaseId filter avoids overlapping non-pot chests.</summary>
public readonly record struct TreasureOpenTarget(Vector3 Position, IReadOnlyList<uint>? PreferredBaseIds = null)
{
    public static implicit operator TreasureOpenTarget(Vector3 position) => new(position);
}

/// <summary>
///     Open a treasure coffer. Interact rules match Pandora's AutoOpenChests;
///     pathing is ours so automation can walk up first.
/// </summary>
public class OpenTreasureCofferChain
(
    IChainFactory chains,
    IObjectTable objects,
    IPlayer player,
    ICondition conditions,
    IVNavmeshIpc vnav
) : ChainRecipe<TreasureOpenTarget>(chains)
{
    /// <summary>Path this close before relying on Pandora's ≤2y interact gate.</summary>
    public const float PathArrivalRange = 1.0f;

    /// <summary>Pandora AutoOpenChests: only Interact when Distance ≤ 2.</summary>
    public const float PreferredOpenDistance = 2.0f;

    /// <summary>Alias used by callers / pot farm approach.</summary>
    public const float InteractDistance = PreferredOpenDistance;

    /// <summary>Keep pathing toward a live coffer while outside Pandora range.</summary>
    public const float MaxInteractRange = PreferredOpenDistance;

    public override string Name => "Open Treasure Coffer";

    protected override IChain Compose(IChain chain, TreasureOpenTarget target)
    {
        var pathState = new PathState();

        return chain
            .UseMiddleware<LogChainMiddleware>()
            .UseStepMiddleware<LogStepMiddleware>()
            .UseStepMiddleware<RunOnMainThreadMiddleware>()
            .WaitUntil(
                _ => ValueTask.FromResult(TryInteract(target, pathState)),
                TimeSpan.FromSeconds(45),
                TimeSpan.FromMilliseconds(200),
                "OpenTreasureCofferChain::Interact"
            );
    }

    private bool TryInteract(TreasureOpenTarget target, PathState pathState)
    {
        if (conditions[ConditionFlag.BetweenAreas]
            || conditions[ConditionFlag.Unconscious])
        {
            return false;
        }

        // Include opened/looted — excluding them made the chain path forever after a successful open (#166).
        IGameObject? nearby = FindMatchingTreasureNear(target, searchRadius: 6f);
        if (nearby != null)
        {
            pathState.SawChest = true;

            unsafe
            {
                GameObject* gameObject = (GameObject*)(void*)nearby.Address;
                var tr = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;

                if (IsOpenedOrLooted(nearby, tr))
                {
                    StopNav();
                    return true;
                }

                float dist3d = Vector3.Distance(player.Position, nearby.Position);
                if (dist3d > PreferredOpenDistance)
                {
                    EnsurePathing(nearby.Position, pathState);
                    return false;
                }

                StopNav();

                // Pandora: require targetable before Interact.
                if (!gameObject->GetIsTargetable())
                {
                    return false;
                }

                if (!EzThrottler.Throttle("ChestThrottle", 200))
                {
                    return false;
                }

                pathState.InteractAttempted = true;
                TargetSystem.Instance()->InteractWithObject(gameObject);
                return IsOpenedOrLooted(nearby, tr);
            }
        }

        // Object gone after we saw / interacted — success (despawned open). Do not treat
        // "standing on pad with no object yet" as done (pot reveals spawn after a short wait).
        if (pathState.SawChest || pathState.InteractAttempted)
        {
            StopNav();
            return true;
        }

        EnsurePathing(target.Position, pathState);
        return false;
    }

    private void StopNav()
    {
        if (vnav.IsRunning() || vnav.IsPathfinding())
        {
            vnav.Stop();
        }
    }

    private void EnsurePathing(Vector3 destination, PathState pathState)
    {
        // Already inside arrival — do not re-queue move-to every tick (vnav spam in #166).
        if (Vector3.Distance(player.Position, destination) <= PathArrivalRange)
        {
            StopNav();
            return;
        }

        const float RepathDrift = 1.5f;
        bool drifted = pathState.LastTarget is not { } last
                       || Vector3.DistanceSquared(last, destination) > RepathDrift * RepathDrift;

        if ((!vnav.IsRunning() && !vnav.IsPathfinding()) || drifted)
        {
            pathState.LastTarget = destination;
            vnav.PathfindAndMoveCloseTo(destination, false, PathArrivalRange);
        }
    }

    /// <summary>Pandora success: Opened/FadedOut flags, or already listed in the Loot window.</summary>
    public static unsafe bool IsOpenedOrLooted(IGameObject chest)
    {
        GameObject* gameObject = (GameObject*)(void*)chest.Address;
        var tr = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
        return IsOpenedOrLooted(chest, tr);
    }

    private static unsafe bool IsOpenedOrLooted(
        IGameObject chest,
        FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* tr
    )
    {
        if (tr->Flags.HasFlag(TreasureFlags.Opened)
            || tr->Flags.HasFlag(TreasureFlags.FadedOut)
            || tr->State is TreasureState.Opened or TreasureState.FadingOut or TreasureState.FadedOut)
        {
            return true;
        }

        Loot* loot = Loot.Instance();
        if (loot == null)
        {
            return false;
        }

        foreach (LootItem item in loot->Items)
        {
            if (item.ChestObjectId == chest.GameObjectId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Matching Treasure near the hunt/pot target (opened/looted included so success can be detected).
    /// </summary>
    private IGameObject? FindMatchingTreasureNear(TreasureOpenTarget target, float searchRadius)
    {
        Vector3 position = target.Position;
        IReadOnlyList<uint>? preferred = target.PreferredBaseIds;

        return objects
            .Where(o =>
            {
                if (!o.IsValid() || o.IsDead)
                {
                    return false;
                }

                if (Vector3.Distance(position, o.Position) > searchRadius)
                {
                    return false;
                }

                if (!MatchesOpenFilter(o, preferred))
                {
                    return false;
                }

                unsafe
                {
                    var obj = (GameObject*)(void*)o.Address;
                    return (CsObjectKind)obj->ObjectKind == CsObjectKind.Treasure;
                }
            })
            .OrderBy(o => Vector3.DistanceSquared(player.Position, o.Position))
            .FirstOrDefault();
    }

    private static bool MatchesOpenFilter(IGameObject obj, IReadOnlyList<uint>? preferredBaseIds)
    {
        if (preferredBaseIds is { Count: > 0 })
        {
            for (int i = 0; i < preferredBaseIds.Count; i++)
            {
                if (obj.BaseId == preferredBaseIds[i])
                {
                    return true;
                }
            }

            return false;
        }

        return obj.ObjectKind == DalamudObjectKind.Treasure;
    }

    private sealed class PathState
    {
        public Vector3? LastTarget;

        public bool SawChest;

        public bool InteractAttempted;
    }
}
