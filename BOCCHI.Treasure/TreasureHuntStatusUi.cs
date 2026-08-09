using BOCCHI.Common.Config;
using BOCCHI.Treasure.Hunt;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Treasure;

/// <summary>Shared hunt progress / Discord resume UX (last coffer id + map flag).</summary>
public static class TreasureHuntStatusUi
{
    /// <summary>1-based coffer progress for the session (counts up as pads are checked).</summary>
    public static string FormatProgress(ITreasureHunter hunter, ITranslator<MainWindow> translator)
    {
        if (hunter.WaitingForSafeWindow)
        {
            return translator.T(".treasure.waiting_safe_window");
        }

        int total = hunter.CheckedCofferCount + hunter.RemainingCofferCount;
        if (total <= 0)
        {
            return hunter.Paused ? translator.T(".treasure.paused") : string.Empty;
        }

        int current = hunter.CheckedCofferCount;
        if (hunter.GetCurrentStep()?.Type == HuntPathfinderStepType.WalkToNode)
        {
            current = Math.Min(current + 1, total);
        }

        string progress = $"{current}/{total}";
        if (hunter.Paused)
        {
            progress = $"{progress} ({translator.T(".treasure.paused")})";
        }

        return progress;
    }

    public static void DrawProgress(
        ITreasureHunter hunter,
        IUIService ui,
        ITranslator<MainWindow> translator,
        TreasureConfig? config = null)
    {
        if (!hunter.Running)
        {
            return;
        }

        if (hunter.WaitingForSafeWindow)
        {
            ImGui.TextWrapped(translator.T(".treasure.waiting_safe_window_detail"));
            return;
        }

        int total = hunter.CheckedCofferCount + hunter.RemainingCofferCount;
        if (total <= 0 && hunter.StepCount <= 0)
        {
            return;
        }

        ui.LabelledValue(translator.T(".treasure.progress"), FormatProgress(hunter, translator));

        if (hunter.LastCheckedNodeId is { } lastId)
        {
            ui.LabelledValue(translator.T(".treasure.last_checked"), lastId.ToString());
        }

        if (hunter.TryGetResumeCoffer(out uint resumeId, out _))
        {
            ui.LabelledValue(translator.T(".treasure.resume_coffer"), resumeId.ToString());
            ImGui.SameLine(0f, 8f);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.SmallButton($"{FontAwesomeIcon.Flag.ToIconString()}##flag_hunt_resume"))
                {
                    hunter.FlagResumePoint();
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(translator.T(".treasure.flag_resume_tooltip"));
            }

            ImGui.SameLine(0f, 8f);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.SmallButton($"{FontAwesomeIcon.LocationArrow.ToIconString()}##recalculate_hunt_route"))
                {
                    hunter.RecalculateRoute();
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(translator.T(".treasure.recalculate_route_tooltip"));
            }
        }

        HuntPathfinderStep? current = hunter.GetCurrentStep();
        if (config != null && current?.Type == HuntPathfinderStepType.WalkToNode)
        {
            ui.LabelledValue(
                translator.T(".treasure.distance_to_chest"),
                $"{hunter.StepDistance:F2}");
        }
    }
}
