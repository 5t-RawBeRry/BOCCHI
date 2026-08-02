using BOCCHI.Automator.Services;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Paths;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using Dalamud.Bindings.ImGui;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Automator;

public class AutomatorRenderer
(
    Func<IAutomator> automatorFactory,
    IAutomatorMemory memory,
    UIConfig uiConfig,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    private IAutomator? automator;

    private IAutomator Automator => automator ??= automatorFactory();

    public MainWindowSection Section => MainWindowSection.Automation;

    public string? SubsectionTitle => translator.T(".automation.automator.title");

    public void Render()
    {
        if (ImGui.Button(Automator.Enabled
            ? translator.T(".automation.automator.disable")
            : translator.T(".automation.automator.enable")))
        {
            Automator.Toggle();
        }

        if (Automator.Enabled)
        {
            ImGui.SameLine();
            if (ImGui.Button(translator.T(".automation.automator.refresh_pathfinding")))
            {
                Automator.RefreshPathfinding();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(translator.T(".automation.automator.refresh_pathfinding_tooltip"));
            }
        }

        if (!Automator.Enabled && !HasDetails())
        {
            return;
        }

        ImGui.Spacing();

        if (ImGui.CollapsingHeader(
            translator.T(".automation.automator.details"),
            HasDetails() ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
        {
            ImGui.Indent();

            if (Automator.Enabled)
            {
                Automator.Render();
            }

            if (memory.TryRemember<GoalMemory>(out GoalMemory goalMemory))
            {
                ui.LabelledValue(translator.T(".status.goal"), GoalFormatHelper.Describe(goalMemory.Goal, translator));
            }

            if (memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory potFarm))
            {
                ui.LabelledValue(translator.T(".automation.automator.pot_chest_farm"), $"Fate {potFarm.FateId.Value}");
                ui.LabelledValue(
                    translator.T(".automation.automator.chests_remaining"),
                    $"{potFarm.RemainingChests}/{potFarm.TotalChests}");
            }

            if (memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory goalPathStepMemory))
            {
                int stepIndex = 1;
                foreach(IPathStep step in goalPathStepMemory.PathSteps)
                {
                    ui.LabelledValue($"{translator.T(".status.current_step")} {stepIndex++}", step.Describe());
                }
            }

            ImGui.Unindent();
        }
    }

    public bool ShouldRender() => uiConfig.ShowAutomationSection;

    private bool HasDetails() =>
        Automator.Enabled
        || memory.TryRemember<GoalMemory>(out GoalMemory _)
        || memory.TryRemember<PotChestFarmMemory>(out PotChestFarmMemory _)
        || memory.TryRemember<GoalPathStepMemory>(out GoalPathStepMemory _);
}
