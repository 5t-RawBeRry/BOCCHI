using BOCCHI.Automator.Data;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.Automator.Services;

/// <summary>
/// Dedicated pots + treasure mode (#114): pot FATEs and chests, then treasure hunt
/// until ~3 minutes before the next pot spawn; preposition and repeat.
/// </summary>
public class PotsTreasureService
(
    IAutomator automator,
    IAutomatorContext context,
    IAutomatorMemory memory,
    ITreasureHunter hunter,
    IPotCycleTracker potCycle,
    IFateRepository fates,
    IZoneProvider zones,
    IAutomationModeGuard modeGuard,
    IChatGui chat,
    ITranslator<MainWindow> translator,
    ILogger<PotsTreasureService> logger
) : IPotsTreasureMode, IOnUpdate, IOnStop
{
    public bool Running => context.IsPotsAndTreasure;

    public PotsTreasurePhase Phase { get; private set; } = PotsTreasurePhase.Off;

    public void OnStop()
    {
        if (Running)
        {
            StopHuntSession();
            automator.TogglePotsAndTreasure();
        }

        Phase = PotsTreasurePhase.Off;
    }

    public void Toggle()
    {
        if (Running)
        {
            StopHuntSession();
            automator.TogglePotsAndTreasure();
            Phase = PotsTreasurePhase.Off;
            return;
        }

        if (!hunter.IsVnavAvailable)
        {
            chat.PrintError(translator.T(".automation.pots_treasure.requires_vnav"));
            return;
        }

        modeGuard.EnsureExclusive(AutomationMode.PotsAndTreasure);

        // Fresh hunt session for this mode (location-resume still applies if coffers remain).
        if (hunter.Running)
        {
            hunter.Toggle();
        }

        automator.TogglePotsAndTreasure();
        hunter.ManagedByPotsTreasure = true;
        Phase = PotsTreasurePhase.DoingPots;
        logger.Info("Pots & Treasure mode started");
    }

    public void Update()
    {
        if (!Running)
        {
            if (Phase != PotsTreasurePhase.Off)
            {
                automator.SetSuspendedForTreasure(false);
                hunter.ManagedByPotsTreasure = false;
                if (hunter.Running)
                {
                    hunter.Toggle();
                }

                Phase = PotsTreasurePhase.Off;
            }

            return;
        }

        hunter.ManagedByPotsTreasure = true;

        if (!zones.GetZone().IsOccultCrescentZone())
        {
            return;
        }

        bool needPots = NeedsPotWork();
        if (needPots)
        {
            EnterPotPhase();
        }
        else
        {
            EnterHuntPhase();
        }
    }

    private void EnterPotPhase()
    {
        Phase = PotsTreasurePhase.DoingPots;
        automator.SetSuspendedForTreasure(false);

        if (hunter.Running && !hunter.Paused)
        {
            hunter.Pause();
            logger.Info("Pots & Treasure: paused hunt for pot window");
        }
    }

    private void EnterHuntPhase()
    {
        Phase = PotsTreasurePhase.Hunting;
        automator.SetSuspendedForTreasure(true);

        if (!hunter.IsVnavReady)
        {
            return;
        }

        if (!hunter.Running)
        {
            hunter.Toggle();
            hunter.ManagedByPotsTreasure = true;
            logger.Info("Pots & Treasure: started treasure hunt filler");
            return;
        }

        if (hunter.Paused)
        {
            hunter.Resume();
            logger.Info("Pots & Treasure: resumed treasure hunt");
        }
    }

    private void StopHuntSession()
    {
        automator.SetSuspendedForTreasure(false);
        hunter.ManagedByPotsTreasure = false;
        if (hunter.Running)
        {
            hunter.Toggle();
        }
    }

    private bool NeedsPotWork()
    {
        if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _))
        {
            return true;
        }

        if (memory.TryRemember<WaitingForPotFateMemory>(out WaitingForPotFateMemory _))
        {
            return true;
        }

        if (memory.TryRemember<GoalMemory>(out GoalMemory goal)
            && goal.Goal.GoalType is FateGoal fateGoal
            && zones.GetZone().IsPotFate(fateGoal.id.Value))
        {
            return true;
        }

        IZone zone = zones.GetZone();
        if (fates.Snapshot().Any(f => zone.IsPotFate(f.Id.Value)))
        {
            return true;
        }

        PotCycleSnapshot cycle = potCycle.Snapshot;
        if (cycle.CurrentActivePotFateId != 0)
        {
            return true;
        }

        if (!cycle.HasPredictedNextPot)
        {
            return false;
        }

        return PotFallbackWindow.ShouldPreposition(
            cycle,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            PotsTreasureDefaults.PrepositionLeadMinutes,
            potFarmingEnabled: true);
    }
}
