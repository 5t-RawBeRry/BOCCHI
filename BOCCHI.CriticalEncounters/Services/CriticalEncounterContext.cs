using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.Extensions;
using Ocelot.Services.Logger;

namespace BOCCHI.CriticalEncounters.Services;

public class CriticalEncounterContext(IObjectTable objects) : ICriticalEncounterContext
{
    public bool IsInCriticalEncounter()
    {
        return GetCriticalEncounterId() != null;
    }

    public unsafe CriticalEncounterId? GetCriticalEncounterId()
    {
        var dec = DynamicEventContainer.GetInstance();
        return dec != null && dec->CurrentEventId != 0 ? new CriticalEncounterId(dec->CurrentEventId) : null;
    }

    public unsafe IEnumerable<IBattleNpc> GetTargets()
    {
        var id = GetCriticalEncounterId();
        if (id == null)
        {
            return [];
        }

        var player = objects.LocalPlayer;
        if (player == null)
        {
            return [];
        }

        var ceId = player.BattleChara()->EventId.EntryId;

        return objects.OfType<IBattleNpc>()
            .Where(obj => obj is { IsDead: false, IsTargetable: true })
            .Where(o => o.IsHostile())
            .Where(o =>
            {
                var battleChara = (BattleChara*)o.Address;

                return o.SubKind == (byte)BattleNpcSubKind.Combatant && battleChara->EventId.EntryId == ceId;
            })
            .OrderBy(o => o.Position.Distance2D(player.Position));
    }
}
