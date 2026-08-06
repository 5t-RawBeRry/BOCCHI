using BOCCHI.Common.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Ocelot.Graphics;
using Ocelot.Services.UI;
using System.Numerics;

namespace BOCCHI.Common.UI;

public static class ActivitySnapshotRenderer
{
    public static void RenderCompact(
        IUIService ui,
        Color? titleColor,
        Color? detailColor,
        string title,
        string details
    )
    {
        ui.Text(title, titleColor);
        ImGui.SameLine(0f, 6f);
        ui.Text(details, detailColor);
    }

    public static void RenderCompactWithActions(
        IUIService ui,
        IActivityNavigation navigation,
        Color? titleColor,
        Color? detailColor,
        string title,
        string details,
        Vector3 destination,
        string actionId,
        bool includeTeleport = true
    )
    {
        RenderCompact(ui, titleColor, detailColor, title, details);

        ImGui.Indent(12f);
        DrawActionButtons(navigation, destination, title, actionId, includeTeleport);
        ImGui.Unindent(12f);
    }

    public static void Render(
        IUIService ui,
        Color? titleColor,
        string title,
        string? titleSuffix,
        params (string Label, object Value)[] fields
    )
    {
        ui.Text(title, titleColor);

        if (!string.IsNullOrEmpty(titleSuffix))
        {
            ImGui.SameLine();
            ImGui.TextUnformatted(titleSuffix);
        }

        ImGui.Indent(32);

        foreach((var label, var value) in fields)
        {
            ui.LabelledValue(label, value);
        }

        ImGui.Unindent(32);
    }

    private static void DrawActionButtons(
        IActivityNavigation navigation,
        Vector3 destination,
        string title,
        string actionId,
        bool includeTeleport
    )
    {
        if (!navigation.CanPathfind)
        {
            return;
        }

        if (IconButton(FontAwesomeIcon.Running, $"path_{actionId}"))
        {
            navigation.PathTo(destination, title, actionId);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"Path to {title} (aethernet when at a shard, then walk)");
        }

        if (!includeTeleport)
        {
            return;
        }

        ImGui.SameLine(0f, 4f);

        bool canTeleport = navigation.CanTeleport(destination, out string? disabledReason);
        using (ImRaii.Disabled(!canTeleport))
        {
            if (IconButton(FontAwesomeIcon.LocationArrow, $"tp_{actionId}"))
            {
                navigation.TeleportToward(destination, title, actionId);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(canTeleport
                ? $"Teleport toward {title} (aethernet only — does not walk)"
                : disabledReason ?? "Teleport unavailable");
        }
    }

    private static bool IconButton(FontAwesomeIcon icon, string id)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            return ImGui.Button($"{icon.ToIconString()}##{id}");
        }
    }
}
