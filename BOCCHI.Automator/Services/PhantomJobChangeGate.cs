using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace BOCCHI.Automator.Services;

/// <summary>Condition gates shared by Illegal Mode job-swap helpers.</summary>
internal static class PhantomJobChangeGate
{
    /// <summary>
    ///     True when ChangeSupportJob is likely to fail with "unable to change phantom jobs".
    /// </summary>
    public static bool IsBlocked(ICondition conditions) =>
        conditions[ConditionFlag.BetweenAreas]
        || conditions[ConditionFlag.BetweenAreas51]
        || conditions[ConditionFlag.Casting]
        || conditions[ConditionFlag.Casting87]
        || conditions[ConditionFlag.Jumping]
        || conditions[ConditionFlag.Jumping61]
        || conditions[ConditionFlag.Occupied]
        || conditions[ConditionFlag.OccupiedInEvent]
        || conditions[ConditionFlag.OccupiedInQuestEvent]
        || conditions[ConditionFlag.OccupiedInCutSceneEvent];
}
