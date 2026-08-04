using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Goals;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.Automator.Services;

/// <summary>
/// Dedicated pots + treasure mode (#114): pot FATEs (and chests) when up / near spawn;
/// soft-pauses treasure hunt as filler between pot windows.
/// </summary>
public class PotsTreasureService
(
    IAutomator automator,
    IAutomatorContext context,
    IAutomatorMemory memory,
    ITreasureHunter hunter,
    IMobFarmer farmer,
    IPotCycleTracker potCycle,
    IFateRepository fates,
    IZoneProvider zones,
    FatesConfig fatesConfig,
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

        if (farmer.Running)
        {
            farmer.Toggle();
        }

        if (context.IsIllegalMode)
        {
            automator.Toggle();
        }

        // Fresh hunt session for this mode (location-resume still applies if coffers remain).
        if (hunter.Running)
        {
            hunter.Toggle();
        }

        if (!hunter.IsVnavAvailable)
        {
            chat.PrintError(translator.T(".automation.pots_treasure.requires_vnav"));
            return;
        }

        automator.TogglePotsAndTreasure();
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
                if (hunter.Running)
                {
                    hunter.Toggle();
                }

                Phase = PotsTreasurePhase.Off;
            }

            return;
        }

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

        // Preposition window — always on for this mode (ignore PreferPotFates / disabled pot IDs).
        if (!cycle.HasPredictedNextPot)
        {
            return false;
        }

        return PotFallbackWindow.ShouldPreposition(
            cycle,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(Math.Max(0, fatesConfig.FateFallbackCutoffMinutes)),
            fatesConfig.PotSpawnLeadMinutes,
            potFarmingEnabled: true);
    }
}
