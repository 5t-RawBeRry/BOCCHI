using BOCCHI.Common.Data.Zones.Graph;
using Dalamud.Bindings.ImGui;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

/// <summary>Shared HashSet&lt;uint&gt; enable/disable checkbox list for zone activity config fields.</summary>
internal static class DisabledActivityIdsHelper
{
    public static bool Render(
        object target,
        PropertyInfo prop,
        Type owner,
        ITranslator translator,
        IUIService ui,
        string rendererName,
        IReadOnlyList<ActivityData> activities,
        Func<uint, string> nameForId,
        Func<uint, string?>? forcedOnReason = null,
        string? forcedOnNote = null,
        string? forcedOnSuffix = null)
    {
        if (prop.PropertyType != typeof(HashSet<uint>))
        {
            throw new InvalidOperationException(
                $"[{rendererName}] must be used on HashSet<uint> properties. " +
                $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        HashSet<uint> disabled = (HashSet<uint>?)prop.GetValue(target) ?? [];
        if (prop.GetValue(target) == null)
        {
            prop.SetValue(target, disabled);
        }

        prop.Label(owner, translator);
        prop.Tooltip(owner, translator);

        if (activities.Count == 0)
        {
            string emptyKey = prop.GetFieldLabelKey(owner).Replace(".label", ".empty", StringComparison.Ordinal);
            ui.Text(translator.T(emptyKey));
            return false;
        }

        if (!string.IsNullOrEmpty(forcedOnNote))
        {
            ui.Text(forcedOnNote);
        }

        bool changed = false;
        foreach (ActivityData activity in activities)
        {
            uint id = (uint)activity.Id;
            string name = nameForId(id);
            string? forcedReason = forcedOnReason?.Invoke(id);
            bool forcedOn = !string.IsNullOrEmpty(forcedReason);
            bool enabled = forcedOn || !disabled.Contains(id);
            string label = forcedOn && !string.IsNullOrEmpty(forcedOnSuffix)
                ? $"{name} (#{activity.Id}){forcedOnSuffix}"
                : $"{name} (#{activity.Id})";

            ImGui.PushID(activity.Id);
            if (forcedOn)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Checkbox(label, ref enabled) && !forcedOn)
            {
                if (enabled)
                {
                    changed |= disabled.Remove(id);
                }
                else
                {
                    changed |= disabled.Add(id);
                }
            }

            if (forcedOn)
            {
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    Ocelot.Extensions.PropertyInfoExtensions.DrawWrappedTooltip(forcedReason!);
                }
            }

            ImGui.PopID();
        }

        if (changed)
        {
            prop.SetValue(target, disabled);
        }

        return changed;
    }
}
