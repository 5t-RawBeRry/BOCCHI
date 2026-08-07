using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Ocelot.Extensions;

namespace BOCCHI.CriticalEncounters.Services;

public class CriticalEncounterContext
(
    IObjectTable objects,
    ICriticalEncounterRepository repository
) : ICriticalEncounterContext
{
    public bool IsInCriticalEncounter() => GetCriticalEncounterId() != null;

    /// <summary>
    ///     Participating in a CE that is in Battle. Uses the player's event id (not the zone
    ///     <c>CurrentEventId</c>), and ignores Register/Warmup so base camp does not show "In CE".
    /// </summary>
    public unsafe CriticalEncounterId? GetCriticalEncounterId()
    {
        IPlayerCharacter? player = objects.LocalPlayer;
        if (player == null)
        {
            return null;
        }

        ushort entryId = player.BattleChara()->EventId.EntryId;
        if (entryId == 0)
        {
            return null;
        }

        CriticalEncounterId id = new(entryId);
        CriticalEncounter? ce = repository.SnapshotWithoutForkedTower().FirstOrDefault(c => c.Id == id);
        if (ce == null || !ce.IsActive())
        {
            return null;
        }

        return id;
    }

    public IEnumerable<IBattleNpc> GetTargets()
    {
        CriticalEncounterId? id = GetCriticalEncounterId();
        return id == null ? [] : GetTargetsFor(id.Value);
    }

    public unsafe IEnumerable<IBattleNpc> GetTargetsFor(CriticalEncounterId id)
    {
        IPlayerCharacter? player = objects.LocalPlayer;
        if (player == null)
        {
            return [];
        }

        ushort ceId = id.Value;

        return objects.OfType<IBattleNpc>()
            .Where(obj => obj is { IsDead: false, IsTargetable: true })
            .Where(o => o.IsHostile())
            .Where(o =>
            {
                BattleChara* battleChara = (BattleChara*)o.Address;

                return o.SubKind == (byte)BattleNpcSubKind.Combatant && battleChara->EventId.EntryId == ceId;
            })
            .OrderBy(o => o.Position.Distance2D(player.Position));
    }

    public bool HasEncounterEnemies(CriticalEncounterId id) => GetTargetsFor(id).Any();
}
