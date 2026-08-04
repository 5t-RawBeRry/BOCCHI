using BOCCHI.Common.Config.Fields;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

/// <summary>
///     Combo for chat sound effects 1–16 (System Config → Sound Effects / &lt;se.#&gt;).
/// </summary>
public sealed class ChatSoundSelectRenderer : IFieldRenderer<ChatSoundSelectAttribute>
{
    public bool Render(object target, PropertyInfo prop, ChatSoundSelectAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(int))
        {
            throw new InvalidOperationException(
                $"[ChatSoundSelectRenderer] must be used on int properties. " +
                $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        int current = Math.Clamp((int)(prop.GetValue(target) ?? 2), 1, 16);
        string label = prop.Label(owner, translator);
        string preview = ResolveName(current, prop, owner, translator);
        bool changed = false;

        if (ImGui.BeginCombo(label, preview))
        {
            for (int id = 1; id <= 16; id++)
            {
                string name = ResolveName(id, prop, owner, translator);
                bool selected = id == current;
                if (ImGui.Selectable(name, selected))
                {
                    current = id;
                    changed = true;
                    PreviewSound(id);
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        prop.Tooltip(owner, translator);

        if (changed)
        {
            prop.SetValue(target, current);
        }

        return changed;
    }

    private static string ResolveName(int id, PropertyInfo prop, Type owner, ITranslator translator)
    {
        string key = prop.GetFieldLabelKey(owner).Replace(".label", $".sounds.{id}", StringComparison.Ordinal);
        return translator.Has(key) ? translator.T(key) : $"Sound Effect {id}";
    }

    private static unsafe void PreviewSound(int soundId)
    {
        try
        {
            UIGlobals.PlaySoundEffect((uint)soundId + 36);
        }
        catch
        {
            // Preview is best-effort only.
        }
    }
}
