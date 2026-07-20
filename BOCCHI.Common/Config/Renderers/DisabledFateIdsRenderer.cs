using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using System.Reflection;
using XIVFate = Lumina.Excel.Sheets.Fate;

namespace BOCCHI.Common.Config.Renderers;

public class DisabledFateIdsRenderer(IZoneProvider zones, IDataManager data, IUIService ui)
    : IFieldRenderer<DisabledFateIdsAttribute>
{
    public bool Render(object target, PropertyInfo prop, DisabledFateIdsAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(HashSet<uint>))
        {
            throw new InvalidOperationException(
                $"[DisabledFateIdsRenderer] must be used on HashSet<uint> properties. " +
                $"{prop.DeclaringType?.Name}.{prop.Name} is {prop.PropertyType.Name}.");
        }

        HashSet<uint> disabled = (HashSet<uint>?)prop.GetValue(target) ?? [];
        if (prop.GetValue(target) == null)
        {
            prop.SetValue(target, disabled);
        }

        prop.Label(owner, translator);
        prop.Tooltip(owner, translator);

        IZone zone = zones.GetZone();
        List<ActivityData> fates = zone.GetNormalFateData()
            .Concat(zone.GetPotFateData())
            .OrderBy(f => f.Id)
            .ToList();

        if (fates.Count == 0)
        {
            string emptyKey = prop.GetFieldLabelKey(owner).Replace(".label", ".empty", StringComparison.Ordinal);
            ui.Text(translator.T(emptyKey));
            return false;
        }

        bool changed = false;
        ExcelSheet<XIVFate> fateSheet = data.GetExcelSheet<XIVFate>();

        foreach(ActivityData fate in fates)
        {
            uint fateId = (uint)fate.Id;
            string name = fateSheet.GetRow(fateId).Name.ToString();
            bool enabled = !disabled.Contains(fateId);

            ImGui.PushID(fate.Id);
            if (ImGui.Checkbox($"{name} (#{fate.Id})", ref enabled))
            {
                if (enabled)
                {
                    changed |= disabled.Remove(fateId);
                }
                else
                {
                    changed |= disabled.Add(fateId);
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
