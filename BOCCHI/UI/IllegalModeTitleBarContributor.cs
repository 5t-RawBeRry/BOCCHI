using System.Numerics;
using BOCCHI.Automator.Services;
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

/// <summary>
///     Restores pre-rewrite Skull (toggle illegal) + Stop (emergency stop) title-bar buttons.
/// </summary>
public class IllegalModeTitleBarContributor(
    IAutomator automator,
    IPathfinder pathfinder,
    IVNavmeshIpc vnav,
    IChainManager chains,
    ITranslator translator
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
            ShowTooltip = () => ImGui.SetTooltip(translator.T("generic.toggle_illegal_mode")),
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
