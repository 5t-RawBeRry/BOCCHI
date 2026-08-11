using BOCCHI.Common.Config.Fields;
using Dalamud.Bindings.ImGui;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using System.Reflection;

namespace BOCCHI.Common.Config.Renderers;

public sealed class TriageRaiseJobRenderer : IFieldRenderer<TriageRaiseJobAttribute>
{
    private const string ChemistKey = "config.automator.fields.preferred_triage_raise_job.chemist";

    private const string WhiteMageKey = "config.automator.fields.preferred_triage_raise_job.white_mage";

    public bool Render(object target, PropertyInfo prop, TriageRaiseJobAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(TriageRaiseJobPreference))
        {
            throw new InvalidOperationException(
                $"[TriageRaiseJobRenderer] must be used on {nameof(TriageRaiseJobPreference)} properties. "
                + $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        if (target is not AutomatorConfig config || !config.EnableTriageMode)
        {
            return false;
        }

        var value = config.PreferredTriageRaiseJob;
        bool changed = false;
        string tooltip = translator.T(prop.GetFieldTooltipKey(owner));

        ImGui.Indent();
        if (ImGui.RadioButton(translator.T(ChemistKey), value == TriageRaiseJobPreference.PhantomChemist))
        {
            value = TriageRaiseJobPreference.PhantomChemist;
            changed = true;
        }

        if (ImGui.IsItemHovered())
        {
            Ocelot.Extensions.PropertyInfoExtensions.DrawWrappedTooltip(tooltip);
        }

        ImGui.SameLine();
        if (ImGui.RadioButton(translator.T(WhiteMageKey), value == TriageRaiseJobPreference.PhantomWhiteMage))
        {
            value = TriageRaiseJobPreference.PhantomWhiteMage;
            changed = true;
        }

        if (ImGui.IsItemHovered())
        {
            Ocelot.Extensions.PropertyInfoExtensions.DrawWrappedTooltip(tooltip);
        }

        ImGui.Unindent();

        if (changed)
        {
            prop.SetValue(target, value);
        }

        return changed;
    }
}
