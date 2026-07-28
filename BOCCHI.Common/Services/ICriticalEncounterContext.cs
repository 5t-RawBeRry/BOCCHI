using BOCCHI.Common.Data.CriticalEncounters;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Ocelot.Extensions;

namespace BOCCHI.Common.Services;

public interface ICriticalEncounterContext
{
    bool IsInCriticalEncounter();

    CriticalEncounterId? GetCriticalEncounterId();

    IEnumerable<IBattleNpc> GetTargets();

    bool IsInZone(IPlayerCharacter player, CriticalEncounter encounter) => player.Position.Distance2D(encounter.Position) <= encounter.Radius;
}
