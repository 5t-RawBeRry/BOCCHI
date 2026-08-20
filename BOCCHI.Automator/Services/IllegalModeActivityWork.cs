using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Shared "is Illegal Mode still busy?" checks for post-activity latches (Triage / Treasure Sight).
/// </summary>
internal static class IllegalModeActivityWork
{
    /// <summary>
    ///     FATE/CE travel and participation. Excludes pot chests, buffs, and Sight
    ///     (those are not "raise after activity" moments).
    /// </summary>
    public static bool HasPrimaryActivity(IAutomatorMemory memory) =>
        memory.TryRemember<GoalMemory>(out GoalMemory _)
        || memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory _)
        || memory.TryRemember<WaitingForPotFateMemory>(out WaitingForPotFateMemory _)
        || memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _)
        || memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _)
        || memory.TryRemember<CommittedCriticalEncounterMemory>(out CommittedCriticalEncounterMemory _);

    /// <summary>Anything that should keep the treasure filler from surveying / hunting.</summary>
    public static bool HasFillerBlockingActivity(IAutomatorMemory memory) =>
        HasPrimaryActivity(memory)
        || memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
        || memory.TryRemember<PendingPotChestFarmMemory>(out PendingPotChestFarmMemory _)
        || memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _)
        || memory.TryRemember<CastingTreasureSightMemory>(out CastingTreasureSightMemory _)
        || TriageSession.IsActive(memory);

    /// <summary>
    ///     Drop wait/path latches (not GoalMemory). Soft-suspend / goal-abort / path refresh.
    /// </summary>
    public static void ForgetTravelLatches(IAutomatorMemory memory, bool includePotChests = false)
    {
        memory.Forget<GoalPathStepMemory>();
        memory.Forget<WaitingForCriticalEncounterMemory>();
        memory.Forget<WaitingForPotFateMemory>();
        memory.Forget<SuspendTravelForActivityMemory>();
        memory.Forget<CommittedCriticalEncounterMemory>();
        if (includePotChests)
        {
            memory.Forget<PotChestFarmMemory>();
        }
    }
}
