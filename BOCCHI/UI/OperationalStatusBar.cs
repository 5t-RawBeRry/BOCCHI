using BOCCHI.Automator.Services;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Services;
using BOCCHI.MobFarmer.Services;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Graphics;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.UI;

public class OperationalStatusBar(
    Func<IAutomator> automatorFactory,
    Func<IMobFarmer> farmerFactory,
    ITreasureHunter hunter,
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
            Automator.CurrentState is { } state ? FormatEnum(state) : null);

        ImGui.SameLine();
        DrawStatusChip(
            translator.T(".status.mob_farmer"),
            Farmer.Running,
            Farmer.Running ? FormatEnum(Farmer.Phase) : null);

        ImGui.SameLine();
        DrawStatusChip(
            translator.T(".status.treasure_hunt"),
            hunter.Running,
            hunter.Running && hunter.StepCount > 0 ? $"{hunter.StepIndex + 1}/{hunter.StepCount}" : null);

        if (memory.TryRemember<GoalMemory>(out var goalMemory))
        {
            ui.LabelledValue(translator.T(".status.goal"), goalMemory.Goal.Describe());
        }

        if (memory.TryRemember<PotChestFarmMemory>(out var potFarm))
        {
            ui.LabelledValue(
                translator.T(".status.chests"),
                $"{potFarm.RemainingChests}/{potFarm.TotalChests} (Fate {potFarm.FateId.Value})");
        }

        if (memory.TryRemember<GoalPathStepMemory>(out var pathMemory)
            && pathMemory.GetNextPathStep() is { } currentStep)
        {
            ui.LabelledValue(translator.T(".status.current_step"), currentStep.Describe());
        }

        ImGui.Separator();
    }

    private void DrawStatusChip(string label, bool active, string? detail)
    {
        var status = active ? translator.T(".status.on") : translator.T(".status.off");
        var color = active ? Color.Green : branding.DalamudGrey;
        ui.Text($"{label}: {status}", color);

        if (!string.IsNullOrEmpty(detail))
        {
            ImGui.SameLine();
            ImGui.TextUnformatted($"({detail})");
        }
    }

    private static string FormatEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return string.Concat(value.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
    }
}
