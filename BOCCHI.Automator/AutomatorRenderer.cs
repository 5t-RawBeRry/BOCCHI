using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Common;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Services.Pathfinding;
using Ocelot.Services.UI;

namespace BOCCHI.Automator;

public class AutomatorRenderer(
    Func<IAutomator> automatorFactory,
    IAutomatorMemory memory,
    IPathfinder pathfinder,
    IUIService ui
) : IDynamicRenderer
{
    private IAutomator? automator;

    private IAutomator Automator => automator ??= automatorFactory();

    public void Render()
    {
        ui.LabelledValue("Automator State", Automator.Enabled);

        if (ImGui.Button("Toggle Automator"))
        {
            Automator.Toggle();
            if (!Automator.Enabled)
            {
                memory.Wipe();
                pathfinder.Stop();
            }
        }

        if (Automator.Enabled)
        {
            Automator.Render();
        }

        if (memory.TryRemember<GoalMemory>(out var goalMemory))
        {
            ui.LabelledValue("Goal", goalMemory.Goal.Describe());
        }

        if (memory.TryRemember<PotChestFarmMemory>(out var potFarm))
        {
            ui.LabelledValue("Pot chest farm", $"Fate {potFarm.FateId.Value}");
            ui.LabelledValue("Chests remaining", $"{potFarm.RemainingChests}/{potFarm.TotalChests}");
        }

        if (memory.TryRemember<GoalPathStepMemory>(out var goalPathStepMemory))
        {
            foreach (var step in goalPathStepMemory.PathSteps)
            {
                ui.LabelledValue("Step", step.Describe());
            }
        }
    }

    public bool ShouldRender()
    {
        return true;
    }
}
