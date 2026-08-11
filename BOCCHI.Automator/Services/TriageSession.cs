using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services;

/// <summary>Pending / active Triage Mode session flags.</summary>
internal static class TriageSession
{
    public static bool IsActive(IAutomatorMemory memory) =>
        memory.TryRemember<PendingTriageMemory>(out PendingTriageMemory _)
        || memory.TryRemember<TriagingMemory>(out TriagingMemory _);

    public static void Clear(IAutomatorMemory memory)
    {
        memory.Forget<PendingTriageMemory>();
        memory.Forget<TriagingMemory>();
    }
}
