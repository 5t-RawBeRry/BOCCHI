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
        Func<uint, string> nameForId)
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

        bool changed = false;
        foreach (ActivityData activity in activities)
        {
            uint id = (uint)activity.Id;
            string name = nameForId(id);
            bool enabled = !disabled.Contains(id);

            ImGui.PushID(activity.Id);
            if (ImGui.Checkbox($"{name} (#{activity.Id})", ref enabled))
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

            ImGui.PopID();
        }

        if (changed)
        {
            prop.SetValue(target, disabled);
        }

        return changed;
    }
}
