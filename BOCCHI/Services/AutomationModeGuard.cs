using BOCCHI.Automator.Services;
using BOCCHI.Buff.Services;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Plugin.Services;
using Ocelot.Chain;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.Services;

public class AutomationModeGuard
(
    Func<IAutomator> automatorFactory,
    Func<IPotsTreasureMode> potsTreasureFactory,
    Func<IMobFarmer> farmerFactory,
    Func<ITreasureHunter> hunterFactory,
    Func<ICarrotHunter> carrotHunterFactory,
    IBuffRunner buffRunner,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    IChainManager chains,
    IChatGui chat,
    UIConfig uiConfig,
    ITranslator<MainWindow> translator
) : IAutomationModeGuard
{
    private IAutomator Automator => automatorFactory();

    private IPotsTreasureMode PotsTreasure => potsTreasureFactory();

    private IMobFarmer Farmer => farmerFactory();

    private ITreasureHunter Hunter => hunterFactory();

    private ICarrotHunter CarrotHunter => carrotHunterFactory();

    private bool stopping;

    public void EnsureExclusive(AutomationMode mode)
    {
        if (stopping)
        {
            return;
        }

        stopping = true;
        try
        {
            // Soft-pause Illegal Mode / Completionist for hunt (resume when hunt ends); don't Toggle off.
            if (mode == AutomationMode.TreasureHunt
                && (Automator.IsIllegalMode || Automator.IsCompletionist))
            {
                Automator.SetSuspendedForTreasure(true);
            }
            else if (mode == AutomationMode.TreasureHunt && Farmer.Running)
            {
                Farmer.SetSuspended(true, FarmerYieldReason.TreasureHunt);
            }
            else if (mode is not AutomationMode.IllegalMode and not AutomationMode.Completionist)
            {
                StopIllegalOrCompletionist();
            }
            else if (mode == AutomationMode.IllegalMode && Automator.IsCompletionist)
            {
                Automator.ToggleCompletionist();
            }
            else if (mode == AutomationMode.Completionist && Automator.IsIllegalMode)
            {
                Automator.Toggle();
            }

            if (mode != AutomationMode.PotsAndTreasure && PotsTreasure.Running)
            {
                if (PotsTreasure.ManagedByMobFarmer)
                {
                    PotsTreasure.StopManagedFromFarmer();
                }
                else
                {
                    PotsTreasure.Toggle();
                }
            }

            if (mode != AutomationMode.MobFarmer && Farmer.Running && mode != AutomationMode.TreasureHunt)
            {
                Farmer.Toggle();
            }

            // Pots & Treasure / Illegal / Completionist / Mob Farmer filler may own the hunter — leave it running.
            if (mode is not AutomationMode.TreasureHunt
                and not AutomationMode.PotsAndTreasure
                and not AutomationMode.IllegalMode
                and not AutomationMode.Completionist
                && Hunter.Running)
            {
                Hunter.Toggle();
            }

            if (mode != AutomationMode.CarrotHunt && CarrotHunter.Running)
            {
                CarrotHunter.Toggle();
            }
        }
        finally
        {
            stopping = false;
        }
    }

    public void NotifyTreasureHuntEnded()
    {
        if (stopping)
        {
            return;
        }

        // Resume Illegal Mode / Completionist — Pots & Treasure manages its own suspension.
        if ((Automator.IsIllegalMode || Automator.IsCompletionist) && Automator.SuspendedForTreasure)
        {
            Automator.SetSuspendedForTreasure(false);
        }

        if (Farmer.Running && Farmer.Suspended && Farmer.YieldReason == FarmerYieldReason.TreasureHunt)
        {
            Farmer.SetSuspended(false);
        }
    }

    public void EmergencyStop()
    {
        if (stopping)
        {
            return;
        }

        stopping = true;
        try
        {
            StopIllegalOrCompletionist();

            if (PotsTreasure.Running)
            {
                PotsTreasure.Toggle();
            }

            if (Farmer.Running)
            {
                Farmer.Toggle();
            }

            if (Hunter.Running)
            {
                Hunter.Toggle();
            }

            if (CarrotHunter.Running)
            {
                CarrotHunter.Toggle();
            }

            if (buffRunner.IsRunning)
            {
                buffRunner.Stop();
            }

            pathfinder.Stop();
            vnav.Stop();
            chains.CancelAll();
            BocchiChat.Print(chat, uiConfig, translator.T(".status.emergency_stop_done"));
        }
        finally
        {
            stopping = false;
        }
    }

    private void StopIllegalOrCompletionist()
    {
        if (Automator.IsIllegalMode)
        {
            Automator.Toggle();
        }
        else if (Automator.IsCompletionist)
        {
            Automator.ToggleCompletionist();
        }
    }
}
