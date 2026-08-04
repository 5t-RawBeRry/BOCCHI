using System.Numerics;
using BOCCHI.Automator.Services;
using BOCCHI.Buff.Services;
using BOCCHI.MobFarmer.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Ocelot.Chain;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.Translation;
using Ocelot.Services.Translation.Extensions;
using Ocelot.Windows;

namespace BOCCHI.UI;

public class IllegalModeTitleBarContributor(
    IAutomator automator,
    IPotsTreasureMode potsTreasure,
    IMobFarmer farmer,
    ITreasureHunter treasureHunter,
    IBuffRunner buffRunner,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    IChainManager chains,
    ITranslator<MainWindow> translator
) : IMainWindowTitleBarContributor
{
    public void Contribute(ICollection<TitleBarButton> buttons)
    {
        buttons.Add(new TitleBarButton
        {
            Click = m =>
            {
                if (m != ImGuiMouseButton.Left)
                {
                    return;
                }

                automator.Toggle();
            },
            Icon = FontAwesomeIcon.Skull,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(translator.T(".automation.automator.toggle_illegal_mode")),
        });

        buttons.Add(new TitleBarButton
        {
            Click = m =>
            {
                if (m != ImGuiMouseButton.Left)
                {
                    return;
                }

                if (automator.Enabled)
                {
                    automator.Toggle();
                }

                if (potsTreasure.Running)
                {
                    potsTreasure.Toggle();
                }

                if (farmer.Running)
                {
                    farmer.Toggle();
                }

                if (treasureHunter.Running)
                {
                    treasureHunter.Toggle();
                }

                if (buffRunner.IsRunning)
                {
                    buffRunner.Stop();
                }

                pathfinder.Stop();
                vnav.Stop();
                chains.CancelAll();
            },
            Icon = FontAwesomeIcon.Stop,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(translator.EmergencyStop()),
        });
    }
}
