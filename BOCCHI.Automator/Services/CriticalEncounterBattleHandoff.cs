using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Shared CE Preparing→Battle handoff signals (EventId lag / CE-tagged enemies).
///     Open-world InCombat and a timed grace alone are not enough — trash near a false
///     wait ring used to flip Illegal Mode into In CE (#196).
/// </summary>
internal static class CriticalEncounterBattleHandoff
{
    public static bool IsReady(
        WaitingForCriticalEncounterMemory wait,
        CriticalEncounterId encounterId,
        ICriticalEncounterContext context)
    {
        wait.MarkBattleStarted();

        return context.GetCriticalEncounterId() == encounterId
               || context.HasEncounterEnemies(encounterId);
    }
}
