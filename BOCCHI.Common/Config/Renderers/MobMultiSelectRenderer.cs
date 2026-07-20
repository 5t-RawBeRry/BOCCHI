using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Mobs;
using BOCCHI.Common.UI;
using Dalamud.Plugin.Services;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using System.Reflection;
namespace BOCCHI.Common.Config.Renderers;

public class MobMultiSelectRenderer(IDataManager data, IUIService ui) : IFieldRenderer<MobMultiSelectAttribute>
{
    private string search = string.Empty;

    public bool Render(object target, PropertyInfo prop, MobMultiSelectAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(List<Mob>))
        {
            throw new InvalidOperationException(
                $"[MobMultiSelectRenderer] must be used on List<{nameof(Mob)}> properties. " +
                $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        List<Mob> mobs = (List<Mob>?)prop.GetValue(target) ?? [];
        if (prop.GetValue(target) == null)
        {
            prop.SetValue(target, mobs);
        }

        prop.Label(owner, translator);
        prop.Tooltip(owner, translator);

        string searchHintKey = prop.GetFieldLabelKey(owner).Replace(".label", ".search_hint", StringComparison.Ordinal);
        string selectedKey = prop.GetFieldLabelKey(owner).Replace(".label", ".selected", StringComparison.Ordinal);
        bool changed = MobPickerHelper.Draw(
            mobs,
            data,
            ui,
            ref search,
            translator.T(searchHintKey),
            translator.T(selectedKey),
            "##config_mob_picker_list",
            220f);

        if (changed)
        {
            prop.SetValue(target, mobs);
        }

        return changed;
    }
}
