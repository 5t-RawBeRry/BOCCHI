using BOCCHI.Buff.Services;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Buff;

public class BuffRenderer
(
    IBuffRunner buffRunner,
    UIConfig uiConfig,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    public uint Order => 0;

    public MainWindowSection Section => MainWindowSection.Trackers;

    public string? SubsectionTitle => translator.T(".buffs.title");

    public void Render()
    {
        bool canStart = buffRunner.CanStart;
        using (ImRaii.Disabled(!canStart && !buffRunner.IsRunning))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button($"{FontAwesomeIcon.Redo.ToIconString()}##apply_buffs"))
                {
                    if (buffRunner.IsRunning)
                    {
                        buffRunner.Stop();
                    }
                    else
                    {
                        buffRunner.Start();
                    }
                }
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            if (buffRunner.IsRunning)
            {
                ImGui.SetTooltip(translator.T(".buffs.stop_tooltip"));
            }
            else if (canStart)
            {
                ImGui.SetTooltip(translator.T(".buffs.apply_tooltip"));
            }
            else
            {
                ImGui.SetTooltip(buffRunner.DisabledReason ?? translator.T(".buffs.apply_tooltip"));
            }
        }

        if (buffRunner.IsRunning)
        {
            ImGui.SameLine();
            ui.Text(translator.T(".buffs.applying"));
        }
    }

    public bool ShouldRender() => uiConfig.ShowBuffSection;
}
