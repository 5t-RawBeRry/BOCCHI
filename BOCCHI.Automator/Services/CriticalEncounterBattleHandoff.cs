using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace BOCCHI.Automator.Services;

/// <summary>Shared CE Preparing→Battle handoff signals (EventId lag / enemies / grace).</summary>
internal static class CriticalEncounterBattleHandoff
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(3);

    public static bool IsReady(
        WaitingForCriticalEncounterMemory wait,
        CriticalEncounterId encounterId,
        ICriticalEncounterContext context,
        ICondition conditions)
    {
        wait.MarkBattleStarted();

        if (context.HasEncounterEnemies(encounterId) || conditions[ConditionFlag.InCombat])
        {
            return true;
        }

        return wait.BattleStartedAtUtc is { } started
               && DateTimeOffset.UtcNow - started >= Grace;
    }
}
