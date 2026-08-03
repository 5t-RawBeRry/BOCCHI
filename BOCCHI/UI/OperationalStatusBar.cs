using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Ocelot.Extensions;
using Ocelot.Graphics;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.UI;

public class OperationalStatusBar
(
    Func<IAutomator> automatorFactory,
    Func<IMobFarmer> farmerFactory,
    ITreasureHunter hunter,
    IBuffRunner buffRunner,
    UIConfig uiConfig,
    IAutomatorMemory memory,
    IBrandingService branding,
    IUIService ui,
    ITranslator<MainWindow> translator
)
{
    private IAutomator? automator;

    private IMobFarmer? farmer;

    private IAutomator Automator => automator ??= automatorFactory();

    private IMobFarmer Farmer => farmer ??= farmerFactory();

    public bool AnyAutomationActive =>
        Automator.Enabled || Farmer.Running || hunter.Running;

    public void Render()
    {
        ImGui.Separator();

        DrawStatusChip(
            translator.T(".status.automator"),
            Automator.Enabled,
            Automator.CurrentState is { } state ? FormatAutomatorState(state) : null);

        ImGui.SameLine();
        DrawStatusChip(
            translator.T(".status.mob_farmer"),
            Farmer.Running,
            Farmer.Running ? FormatFarmerPhase(Farmer.Phase) : null);

        ImGui.SameLine();
        DrawStatusChip(
            translator.T(".status.treasure_hunt"),
            hunter.Running,
            hunter.Running
                ? hunter.Paused
                    ? translator.T(".treasure.paused")
                    : hunter.StepCount > 0 ? $"{hunter.StepIndex + 1}/{hunter.StepCount}" : null
                : null);

        if (uiConfig.ShowBuffSection)
        {
            ImGui.SameLine(0f, 16f);
            DrawBuffAction();
        }

        if (memory.TryRemember<GoalMemory>(out GoalMemory goalMemory))
        {
            ui.LabelledValue(translator.T(".status.goal"), GoalFormatHelper.Describe(goalMemory.Goal, translator));
        }

        if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory potFarm))
        {
            ui.LabelledValue(
                translator.T(".status.chests"),
                $"{potFarm.RemainingChests}/{potFarm.TotalChests} (Fate {potFarm.FateId.Value})");
        }

        if (memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory pathMemory)
            && pathMemory.GetNextPathStep() is { } currentStep)
        {
            ui.LabelledValue(translator.T(".status.current_step"), currentStep.Describe());
        }

        ImGui.Separator();
    }

    private void DrawBuffAction()
    {
        ui.Text(translator.T(".buffs.title"), branding.DalamudYellow);
        ImGui.SameLine(0f, 6f);

        bool canStart = buffRunner.CanStart;
        using (ImRaii.Disabled(!canStart && !buffRunner.IsRunning))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                // Magic wand reads clearer than Redo for "apply buffs".
                if (ImGui.SmallButton($"{FontAwesomeIcon.Magic.ToIconString()}##apply_buffs"))
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
            ImGui.SameLine(0f, 6f);
            ui.Text(translator.T(".buffs.applying"), branding.DalamudGrey);
        }
    }

    private void DrawStatusChip(string label, bool active, string? detail)
    {
        string status = active ? translator.T(".status.on") : translator.T(".status.off");
        Color color = active ? Color.Green : branding.DalamudGrey;
        ui.Text($"{label}: {status}", color);

        if (!string.IsNullOrEmpty(detail))
        {
            ImGui.SameLine();
            ImGui.TextUnformatted($"({detail})");
        }
    }

    private string FormatAutomatorState(AutomatorState state) =>
        translator.T($".status.automator_states.{state.ToString().ToSnakeCase()}");

    private string FormatFarmerPhase(FarmerPhase phase) =>
        translator.T($".status.farmer_phases.{phase.ToString().ToSnakeCase()}");
}
