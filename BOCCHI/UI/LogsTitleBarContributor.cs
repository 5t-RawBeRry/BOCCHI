using BOCCHI.Services.Logging;
using BOCCHI.Common.Services.Logging;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Ocelot.Services.Translation;
using Ocelot.Windows;
using System.Numerics;

namespace BOCCHI.UI;

public class LogsTitleBarContributor(
    ILogsWindow logWindow,
    IBocchiLogClipboard clipboard,
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

                logWindow.Toggle();
            },
            Icon = FontAwesomeIcon.List,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(translator.T(".logs.open_tooltip")),
        });

        buttons.Add(new TitleBarButton
        {
            Click = m =>
            {
                if (m != ImGuiMouseButton.Left)
                {
                    return;
                }

                clipboard.CopyAll(announceInChat: true);
            },
            Icon = FontAwesomeIcon.Clipboard,
            IconOffset = new Vector2(2, 2),
            ShowTooltip = () => ImGui.SetTooltip(translator.T(".logs.copy_all_tooltip")),
        });
    }
}
