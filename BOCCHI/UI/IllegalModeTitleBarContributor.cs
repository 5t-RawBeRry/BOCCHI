using System.Numerics;
using BOCCHI.Automator.Services;
using BOCCHI.Common.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.UI;

public class IllegalModeTitleBarContributor(
    IAutomator automator,
    IAutomationModeGuard modeGuard,
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

                modeGuard.EmergencyStop();
            },
            Icon = FontAwesomeIcon.Stop,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(translator.T(".status.emergency_stop_tooltip")),
        });
    }
}
