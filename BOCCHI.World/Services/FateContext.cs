using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Extensions;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using Ocelot.Extensions;

namespace BOCCHI.Fates.Services;

public class FateContext(IObjectTable objects) : IFateContext
{
    public bool IsInFate() => GetFateId() != null;

    public unsafe FateId? GetFateId()
    {
        FateManager* fateManager = FateManager.Instance();

        return fateManager != null && fateManager->CurrentFate != null ? new FateId(fateManager->CurrentFate->FateId) : null;
    }

    public IEnumerable<IBattleNpc> GetTargets()
    {
        FateId? id = GetFateId();
        if (id == null)
        {
            return [];
        }

        IPlayerCharacter? player = objects.LocalPlayer;
        if (player == null)
        {
            return [];
        }

        ushort fateId = id.Value.Value;

        return objects.OfType<IBattleNpc>()
            .Where(obj => obj is { IsDead: false, IsTargetable: true })
            .Where(o => o.IsHostile())
            .Where(obj => NpcBelongsToFate(obj, fateId))
            .OrderBy(o => o.Position.Distance2D(player.Position));
    }

    public bool IsInCombatWith(FateId id)
    {
        IPlayerCharacter? player = objects.LocalPlayer;
        if (player == null)
        {
            return false;
        }

        ulong? targetId = player.TargetObject?.GameObjectId;

        foreach (IBattleNpc npc in objects.OfType<IBattleNpc>())
        {
            if (npc is not { IsDead: false, IsTargetable: true } || !npc.IsHostile())
            {
                continue;
            }

            if (!NpcBelongsToFate(npc, id.Value))
            {
                continue;
            }

            if (targetId is { } idValue && npc.GameObjectId == idValue)
            {
                return true;
            }

            if (npc.IsTargetingPlayer(player))
            {
                return true;
            }
        }

        return false;
    }

    private static unsafe bool NpcBelongsToFate(IBattleNpc npc, ushort fateId)
    {
        BattleChara* battleChara = (BattleChara*)npc.Address;
        return battleChara->FateId == fateId;
    }
}
