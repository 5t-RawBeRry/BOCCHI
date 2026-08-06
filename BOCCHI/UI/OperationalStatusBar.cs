using BOCCHI.Automator.Data;
using BOCCHI.Automator.Services;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.StateMemory;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using BOCCHI.Common.UI;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using BOCCHI.Treasure;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;
using Ocelot.Graphics;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.UI;

public class OperationalStatusBar
(
    Func<IAutomator> automatorFactory,
    Func<IPotsTreasureMode> potsTreasureFactory,
    Func<IMobFarmer> farmerFactory,
    ITreasureHunter hunter,
    IBuffRunner buffRunner,
    UIConfig uiConfig,
    IAutomatorMemory memory,
    IPotCycleTracker potCycle,
    IDataManager data,
    IBrandingService branding,
    IUIService ui,
    ITranslator<MainWindow> translator
)
{
    private IAutomator? automator;

    private IPotsTreasureMode? potsTreasure;

    private IMobFarmer? farmer;

    private IAutomator Automator => automator ??= automatorFactory();

    private IPotsTreasureMode PotsTreasure => potsTreasure ??= potsTreasureFactory();

    private IMobFarmer Farmer => farmer ??= farmerFactory();

    public bool IllegalModeActive => Automator.Enabled;

    public bool PotsTreasureActive => PotsTreasure.Running;

    public bool MobFarmerActive => Farmer.Running;

    public bool TreasureHuntActive => hunter.Running;

    public bool StandaloneTreasureHuntActive => hunter.Running && !hunter.ManagedByPotsTreasure;

    public bool AnyAutomationActive =>
        IllegalModeActive || PotsTreasureActive || MobFarmerActive || TreasureHuntActive;

    public void Render()
    {
        ImGui.Separator();

        bool anyMode = IllegalModeActive || PotsTreasureActive || MobFarmerActive || StandaloneTreasureHuntActive;
        if (!anyMode)
        {
            ui.Text(translator.T(".status.idle"), branding.DalamudGrey);
        }
        else
        {
            bool first = true;
            void Chip(string label, string? detail)
            {
                if (!first)
                {
                    ImGui.SameLine();
                }

                first = false;
                DrawActiveChip(label, detail);
            }

            if (IllegalModeActive)
            {
                Chip(
                    translator.T(".status.automator"),
                    Automator.CurrentState is { } state ? FormatAutomatorState(state) : null);
            }

            if (PotsTreasureActive)
            {
                string phase = translator.T(
                    $".automation.pots_treasure.phases.{PotsTreasure.Phase.ToString().ToSnakeCase()}");
                string? detail = phase;
                if (PotsTreasure.Phase == PotsTreasurePhase.Hunting
                    && hunter.Running
                    && (hunter.StepCount > 0 || hunter.WaitingForSafeWindow))
                {
                    detail = $"{phase} · {TreasureHuntStatusUi.FormatProgress(hunter, translator)}";
                }

                Chip(translator.T(".status.pots_treasure"), detail);
            }

            if (MobFarmerActive)
            {
                Chip(translator.T(".status.mob_farmer"), FormatFarmerPhase(Farmer.Phase));
            }

            if (StandaloneTreasureHuntActive)
            {
                Chip(
                    translator.T(".status.treasure_hunt"),
                    TreasureHuntStatusUi.FormatProgress(hunter, translator));
            }
        }

        string? potChip = PotTimerUi.FormatCompact(potCycle, data, translator);
        if (potChip != null)
        {
            ImGui.SameLine(0f, 16f);
            ui.Text(potChip, branding.DalamudGrey);
        }

        // Own row — not a mode chip; knowledge-crystal buff apply/stop.
        if (uiConfig.ShowBuffSection)
        {
            DrawBuffAction();
        }

        // Goal / pot chests only while Illegal Mode or Pots phase is driving the automator.
        bool showGoalRows = IllegalModeActive
                            || (PotsTreasureActive && PotsTreasure.Phase == PotsTreasurePhase.DoingPots);
        if (showGoalRows)
        {
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
        }

        ImGui.Separator();
    }

    private void DrawBuffAction()
    {
        bool canStart = buffRunner.CanStart;
        bool running = buffRunner.IsRunning;
        string label = running
            ? translator.T(".buffs.stop_button")
            : translator.T(".buffs.apply_button");

        using (ImRaii.Disabled(!canStart && !running))
        {
            // Flask reads as buffs/potions; Magic looked like a random pill next to status chips.
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(FontAwesomeIcon.Flask.ToIconString());
            }

            ImGui.SameLine(0f, 6f);
            if (ImGui.SmallButton($"{label}##buffs_action"))
            {
                if (running)
                {
                    buffRunner.Stop();
                }
                else
                {
                    buffRunner.Start();
                }
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            if (running)
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

        if (running)
        {
            ImGui.SameLine(0f, 8f);
            ui.Text(translator.T(".buffs.applying"), branding.DalamudGrey);
        }
    }

    private void DrawActiveChip(string label, string? detail)
    {
        ui.Text($"{label}: {translator.T(".status.on")}", Color.Green);

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
