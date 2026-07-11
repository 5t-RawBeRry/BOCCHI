using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ChoosingActivityHandler(
    IAutomatorMemory memory,
    ICriticalEncounterRepository criticalEncounterRepository,
    IFateRepository fateRepository,
    IGoalFactory goalFactory,
    IBuffProvider buffs,
    BuffConfig buffConfig,
    FatesConfig fatesConfig,
    IFateScorer fateScorer
) : ScoreStateHandler<AutomatorState, StatePriority>(AutomatorState.ChoosingActivity)
{
    public override StatePriority GetScore()
    {
        if (memory.TryRemember<GoalMemory>(out var _))
        {
            return StatePriority.Never;
        }

        if (buffConfig.ShouldAutomateBuffs && buffs.ShouldRefreshAny())
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<ApplyingBuffsMemory>(out _))
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<PotChestFarmMemory>(out _))
        {
            return StatePriority.Never;
        }

        var enabledFates = fateRepository.Snapshot().Count(f => fatesConfig.IsFateEnabled(f.Id.Value));
        var criticalEncounters = criticalEncounterRepository.SnapshotWithoutForkedTower().Count(ce => ce.State == DynamicEventState.Register);

        if (enabledFates <= 0 && criticalEncounters <= 0)
        {
            return StatePriority.Never;
        }

        return StatePriority.VeryLow;
    }

    public override void Handle()
    {
        var criticalEncounter = criticalEncounterRepository.SnapshotWithoutForkedTower().FirstOrDefault(c => c.State == DynamicEventState.Register);
        if (criticalEncounter != null)
        {
            var goal = goalFactory.CriticalEncounter(criticalEncounter.Id);
            memory.TryAdd(new GoalMemory(goal));
            return;
        }

        var fate = fateScorer.SelectBest(fateRepository.Snapshot());
        if (fate != null)
        {
            var goal = goalFactory.Fate(fate.Id);
            memory.TryAdd(new GoalMemory(goal));
            return;
        }

        throw new Exception("Unable to determine a goal in the Choosing activity state...");
    }
}
