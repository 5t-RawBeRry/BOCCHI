using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.UI;

public class KofiTitleBarContributor(ITranslator<MainWindow> translator) : IMainWindowTitleBarContributor
{
    private const string KofiUrl = "https://ko-fi.com/kagekazu";

    public void Contribute(ICollection<TitleBarButton> buttons)
    {
        buttons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Heart,
            IconOffset = new Vector2(1, 1),
            ShowTooltip = () => ImGui.SetTooltip(translator.T(".support.kofi_tooltip")),
            Click = m =>
            {
                if (m != ImGuiMouseButton.Left)
                {
                    return;
                }

                OpenKofi();
            },
        });
    }

    private static void OpenKofi()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = KofiUrl,
            UseShellExecute = true,
        });
    }
}
