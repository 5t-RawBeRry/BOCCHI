using System.Numerics;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Shared CE Preparing→Battle handoff signals (EventId lag / CE-tagged enemies).
///     Open-world InCombat alone is not enough — trash near a false wait ring used to flip
///     Illegal Mode into In CE (#196). After #196, requiring only EventId/enemies left players
///     stuck on Waiting for CE (no BossMod preset) when those signals lagged after Battle.
/// </summary>
internal static class CriticalEncounterBattleHandoff
{
    public static bool IsReady(
        WaitingForCriticalEncounterMemory wait,
        CriticalEncounter encounter,
        ICriticalEncounterContext context,
        Vector3 playerPosition)
    {
        wait.MarkBattleStarted();

        if (context.GetCriticalEncounterId() == encounter.Id
            || context.HasEncounterEnemies(encounter.Id))
        {
            return true;
        }

        // Already waiting for this CE, battle started, still inside the registration edge —
        // EventId / enemy tags can lag for seconds after Warmup→Battle.
        if (!encounter.IsActive())
        {
            return false;
        }

        float combatRadius = NavigationConstants.CriticalEncounterRedRadius(
            encounter.Radius,
            encounter.AreaShape);
        return NavigationConstants.IsInsideCriticalEncounterRegistrationArea(
            encounter.Position,
            combatRadius,
            encounter.AreaShape,
            playerPosition);
    }
}
