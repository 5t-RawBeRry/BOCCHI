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

namespace BOCCHI.Treasure.ChainRecipes;

/// <summary>Where to open a coffer; optional BaseId filter avoids overlapping non-pot chests.</summary>
public readonly record struct TreasureOpenTarget(Vector3 Position, IReadOnlyList<uint>? PreferredBaseIds = null)
{
    public static implicit operator TreasureOpenTarget(Vector3 position) => new(position);
}

/// <summary>
///     Open a treasure coffer using the same interact rules as Pandora's AutoOpenChests.
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
    public const float PathArrivalRange = 1.0f;

    /// <summary>Pandora AutoOpenChests uses 3D distance ≤ 2y.</summary>
    public const float PreferredOpenDistance = 2.0f;

    public const float MaxInteractRange = 2.75f;

    public const float InteractDistance = PreferredOpenDistance;

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
        // Match Pandora's ChestThrottle cadence.
        if (!EzThrottler.Throttle("ChestInteract", 200))
        {
            return false;
        }

        if (conditions[ConditionFlag.BetweenAreas]
            || conditions[ConditionFlag.Unconscious])
        {
            return false;
        }

        IGameObject? chest = GetChestAt(target);
        if (chest == null)
        {
            return false;
        }

        unsafe
        {
            GameObject* gameObject = (GameObject*)(void*)chest.Address;
            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure* tr =
                (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;

            if (IsOpenedOrLooted(chest, tr))
            {
                return true;
            }

            if (tr->State is TreasureState.Opening)
            {
                return false;
            }

            float dist3d = Vector3.Distance(player.Position, chest.Position);
            if (dist3d > PreferredOpenDistance || !gameObject->GetIsTargetable())
            {
                EnsurePathing(chest.Position, pathState);
                return false;
            }

            if (vnav.IsRunning())
            {
                vnav.Stop();
            }

            pathState.LastTarget = null;

            // Pandora does not pre-target; InteractWithObject with default LoS check.
            TargetSystem.Instance()->InteractWithObject(gameObject);
            return IsOpenedOrLooted(chest, tr);
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

    private IGameObject? GetChestAt(TreasureOpenTarget target)
    {
        const float SearchRadius = 6f;
        Vector3 position = target.Position;
        IReadOnlyList<uint>? preferred = target.PreferredBaseIds;

        IEnumerable<IGameObject> candidates = objects.Where(o =>
            o.IsValid()
            && !o.IsDead
            && Vector3.Distance(position, o.Position) <= SearchRadius
            && MatchesOpenFilter(o, preferred));

        return candidates
            .OrderBy(o => Vector3.DistanceSquared(position, o.Position))
            .FirstOrDefault();
    }

    private static bool MatchesOpenFilter(IGameObject obj, IReadOnlyList<uint>? preferredBaseIds)
    {
        if (preferredBaseIds is { Count: > 0 })
        {
            // Pot farm: only Magic Pot reveal coffers (overlap with bronze/silver layout coffers).
            for (int i = 0; i < preferredBaseIds.Count; i++)
            {
                if (obj.BaseId == preferredBaseIds[i])
                {
                    return true;
                }
            }

            return false;
        }

        // Normal treasure hunt: any live Treasure object (Pandora-style).
        return obj.ObjectKind == DalamudObjectKind.Treasure;
    }

    private sealed class PathState
    {
        public Vector3? LastTarget;
    }
}
