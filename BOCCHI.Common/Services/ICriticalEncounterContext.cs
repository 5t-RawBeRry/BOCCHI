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

    /// <summary>Hostiles tagged with this CE EventId (works even if the local player EventId is unset).</summary>
    IEnumerable<IBattleNpc> GetTargetsFor(CriticalEncounterId id);

    bool HasEncounterEnemies(CriticalEncounterId id);

    bool IsInZone(IPlayerCharacter player, CriticalEncounter encounter) =>
        player.Position.Distance2D(encounter.RegistrationCenter) <= encounter.Radius;
}
