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
        // Pandora: BetweenAreas bail; throttle 200ms on the interact attempt.
        if (conditions[ConditionFlag.BetweenAreas]
            || conditions[ConditionFlag.Unconscious])
        {
            return false;
        }

        IGameObject? nearby = FindLiveChestNear(target, searchRadius: 6f);
        if (nearby == null)
        {
            EnsurePathing(target.Position, pathState);
            return false;
        }

        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)nearby.Address;
            var tr = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;

            if (IsOpenedOrLooted(nearby, tr))
            {
                if (vnav.IsRunning())
                {
                    vnav.Stop();
                }

                return true;
            }

            float dist3d = Vector3.Distance(player.Position, nearby.Position);

            // Not yet in Pandora's 2y window — keep walking in (automation-only; Pandora is passive).
            if (dist3d > PreferredOpenDistance)
            {
                EnsurePathing(nearby.Position, pathState);
                return false;
            }

            if (vnav.IsRunning() || vnav.IsPathfinding())
            {
                vnav.Stop();
            }

            // Pandora: require targetable before Interact — avoids "Too far away" spam while approaching.
            if (!gameObject->GetIsTargetable())
            {
                return false;
            }

            if (!EzThrottler.Throttle("ChestThrottle", 200))
            {
                return false;
            }

            // Pandora calls the single-arg overload (default LoS check).
            TargetSystem.Instance()->InteractWithObject(gameObject);
            return IsOpenedOrLooted(nearby, tr);
        }
    }

    private void EnsurePathing(Vector3 destination, PathState pathState)
    {
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
    /// Live coffer near the hunt/pot target. ObjectKind check matches Pandora (CS Treasure).
    /// Targetable / ≤2y are applied at Interact time, not here — so we can still path in.
    /// </summary>
    private IGameObject? FindLiveChestNear(TreasureOpenTarget target, float searchRadius)
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
                    if ((CsObjectKind)obj->ObjectKind != CsObjectKind.Treasure)
                    {
                        return false;
                    }

                    var tr = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)obj;
                    if (tr->Flags.HasFlag(TreasureFlags.Opened)
                        || tr->Flags.HasFlag(TreasureFlags.FadedOut))
                    {
                        return false;
                    }

                    Loot* loot = Loot.Instance();
                    if (loot != null)
                    {
                        foreach (LootItem item in loot->Items)
                        {
                            if (item.ChestObjectId == o.GameObjectId)
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
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
    }
}
