using BOCCHI.Automator.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Ocelot.States.Score;

namespace BOCCHI.Automator.StateMachine.Handlers;

public class ChoosingActivityHandler
(
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
        if (memory.TryRemember<GoalMemory>(out GoalMemory _))
        {
            return StatePriority.Never;
        }

        if (buffConfig.ShouldAutomateBuffs && buffs.ShouldRefreshAny())
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<ApplyingBuffsMemory>(out ApplyingBuffsMemory _))
        {
            return StatePriority.Never;
        }

        if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _))
        {
            return StatePriority.Never;
        }

        int enabledFates = fateRepository.Snapshot().Count(f => fatesConfig.IsFateEnabled(f.Id.Value));
        int criticalEncounters = criticalEncounterRepository.SnapshotWithoutForkedTower().Count(ce => ce.State == DynamicEventState.Register);

        if (enabledFates <= 0 && criticalEncounters <= 0)
        {
            return StatePriority.Never;
        }

        return StatePriority.VeryLow;
    }

    public override void Handle()
    {
        CriticalEncounter? criticalEncounter = criticalEncounterRepository.SnapshotWithoutForkedTower().FirstOrDefault(c => c.State == DynamicEventState.Register);
        if (criticalEncounter != null)
        {
            IGoal goal = goalFactory.CriticalEncounter(criticalEncounter.Id);
            memory.TryAdd(new GoalMemory(goal));
            return;
        }

        Fate? fate = fateScorer.SelectBest(fateRepository.Snapshot());
        if (fate != null)
        {
            IGoal goal = goalFactory.Fate(fate.Id);
            memory.TryAdd(new GoalMemory(goal));
            return;
        }

        throw new("Unable to determine a goal in the Choosing activity state...");
    }
}
