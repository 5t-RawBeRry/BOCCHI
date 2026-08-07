using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Latches Triage Mode when Illegal Mode finishes a FATE/CE (independent of treasure filler).
/// </summary>
public sealed class TriageLatchService
(
    IAutomatorContext context,
    IAutomatorMemory memory,
    ISupportJobFactory supportJobs,
    IZoneProvider zones,
    AutomatorConfig automatorConfig,
    ILogger<TriageLatchService> logger
) : IOnUpdate
{
    // Before IllegalModeTreasureFillerService so Sight waits for PendingTriage.
    public int Order => 10;

    private bool hadActivity;

    public void Update()
    {
        if (!context.IsIllegalMode || !automatorConfig.EnableTriageMode)
        {
            hadActivity = false;
            if (!automatorConfig.EnableTriageMode)
            {
                memory.Forget<PendingTriageMemory>();
            }

            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        bool activityNow = HasActivityWork();
        if (hadActivity && !activityNow)
        {
            TryLatch();
        }

        hadActivity = activityNow;
    }

    private void TryLatch()
    {
        if (memory.TryRemember<PendingTriageMemory>(out PendingTriageMemory _)
            || memory.TryRemember<TriagingMemory>(out TriagingMemory _))
        {
            return;
        }

        SupportJob chemist = supportJobs.Create(SupportJobId.PhantomChemist);
        if (chemist.Level < 1)
        {
            logger.Info("Triage Mode skipped — Phantom Chemist not unlocked");
            return;
        }

        memory.TryAdd(new PendingTriageMemory());
        logger.Info("Triage Mode latched after activity");
    }

    private bool HasActivityWork()
    {
        if (memory.TryRemember<GoalMemory>(out GoalMemory _))
        {
            return true;
        }

        if (memory.TryRemember<WaitingForCriticalEncounterMemory>(out WaitingForCriticalEncounterMemory _)
            || memory.TryRemember<WaitingForPotFateMemory>(out WaitingForPotFateMemory _)
            || memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _)
            || memory.TryRemember<SuspendTravelForActivityMemory>(out SuspendTravelForActivityMemory _))
        {
            return true;
        }

        // Pot chests / buffs are not "raise after FATE" moments.
        return false;
    }
}
