using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Automator;

public class AutomatorRenderer(
    Func<IAutomator> automatorFactory,
    IAutomatorMemory memory,
    IPathfinder pathfinder,
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
            if (!Automator.Enabled)
            {
                memory.Wipe();
                pathfinder.Stop();
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

            if (memory.TryRemember<GoalMemory>(out var goalMemory))
            {
                ui.LabelledValue(translator.T(".status.goal"), goalMemory.Goal.Describe());
            }

            if (memory.TryRemember<PotChestFarmMemory>(out var potFarm))
            {
                ui.LabelledValue(translator.T(".automation.automator.pot_chest_farm"), $"Fate {potFarm.FateId.Value}");
                ui.LabelledValue(
                    translator.T(".automation.automator.chests_remaining"),
                    $"{potFarm.RemainingChests}/{potFarm.TotalChests}");
            }

            if (memory.TryRemember<GoalPathStepMemory>(out var goalPathStepMemory))
            {
                var stepIndex = 1;
                foreach (var step in goalPathStepMemory.PathSteps)
                {
                    ui.LabelledValue($"{translator.T(".status.current_step")} {stepIndex++}", step.Describe());
                }
            }

            ImGui.Unindent();
        }
    }

    public bool ShouldRender()
    {
        return uiConfig.ShowAutomationSection;
    }

    private bool HasDetails()
    {
        return Automator.Enabled
               || memory.TryRemember<GoalMemory>(out _)
               || memory.TryRemember<PotChestFarmMemory>(out _)
               || memory.TryRemember<GoalPathStepMemory>(out _);
    }
}
