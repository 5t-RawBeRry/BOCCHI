using BOCCHI.Automator.Services;
using BOCCHI.Buff.Services;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Services;
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
            // Soft-pause Illegal Mode for hunt (resume when hunt ends); don't Toggle off.
            if (mode == AutomationMode.TreasureHunt && Automator.IsIllegalMode)
            {
                Automator.SetSuspendedForTreasure(true);
            }
            else if (mode != AutomationMode.IllegalMode && Automator.Enabled)
            {
                Automator.Toggle();
            }

            if (mode != AutomationMode.PotsAndTreasure && PotsTreasure.Running)
            {
                PotsTreasure.Toggle();
            }

            if (mode != AutomationMode.MobFarmer && Farmer.Running)
            {
                Farmer.Toggle();
            }

            // Pots & Treasure or Illegal Mode filler owns the hunter — leave it running when entering that mode.
            if (mode is not AutomationMode.TreasureHunt and not AutomationMode.PotsAndTreasure
                and not AutomationMode.IllegalMode
                && Hunter.Running)
            {
                Hunter.Toggle();
            }
        }
        finally
        {
            stopping = false;
        }
    }

    public void NotifyStandaloneTreasureHuntEnded()
    {
        if (stopping)
        {
            return;
        }

        // Resume Illegal Mode only — Pots & Treasure manages its own suspension.
        if (Automator.IsIllegalMode && Automator.SuspendedForTreasure)
        {
            Automator.SetSuspendedForTreasure(false);
        }
    }

    public void NotifyIllegalModeFillerHuntEnded()
    {
        if (stopping)
        {
            return;
        }

        if (Automator.IsIllegalMode && Automator.SuspendedForTreasure)
        {
            Automator.SetSuspendedForTreasure(false);
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
            if (Automator.Enabled)
            {
                Automator.Toggle();
            }

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

            if (buffRunner.IsRunning)
            {
                buffRunner.Stop();
            }

            pathfinder.Stop();
            vnav.Stop();
            chains.CancelAll();
            chat.Print(BocchiChat.Format(translator.T(".status.emergency_stop_done"), uiConfig));
        }
        finally
        {
            stopping = false;
        }
    }
}
