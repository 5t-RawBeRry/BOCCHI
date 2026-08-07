using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;

namespace BOCCHI.Automator.Services;

/// <summary>
///     After FATE/CE: latch Triage only when a raisable corpse is already nearby.
///     No bodies → normal Return continues.
/// </summary>
public sealed class TriageLatchService
(
    IAutomatorContext context,
    IAutomatorMemory memory,
    ISupportJobFactory supportJobs,
    IZoneProvider zones,
    IObjectTable objects,
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
                TriageSession.Clear(memory);
            }

            return;
        }

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        bool activityNow = IllegalModeActivityWork.HasPrimaryActivity(memory);
        if (hadActivity && !activityNow)
        {
            TryLatch();
        }

        hadActivity = activityNow;
    }

    private void TryLatch()
    {
        if (TriageSession.IsActive(memory))
        {
            return;
        }

        if (!SupportJobChemist.IsUnlocked(supportJobs))
        {
            logger.Info("Triage Mode skipped — Phantom Chemist not unlocked");
            return;
        }

        if (!RaiseableCorpses.Any(objects))
        {
            return;
        }

        memory.TryAdd(new PendingTriageMemory());
        logger.Info("Triage Mode latched — raisable targets nearby");
    }
}
