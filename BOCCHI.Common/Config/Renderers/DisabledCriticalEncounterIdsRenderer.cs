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
using XIVDynamicEvent = Lumina.Excel.Sheets.DynamicEvent;

namespace BOCCHI.Common.Config.Renderers;

public class DisabledCriticalEncounterIdsRenderer(IZoneProvider zones, IDataManager data, IUIService ui)
    : IFieldRenderer<DisabledCriticalEncounterIdsAttribute>
{
    public bool Render(object target, PropertyInfo prop, DisabledCriticalEncounterIdsAttribute attr, Type owner, ITranslator translator)
    {
        if (prop.PropertyType != typeof(HashSet<uint>))
        {
            throw new InvalidOperationException(
                $"[DisabledCriticalEncounterIdsRenderer] must be used on HashSet<uint> properties. " +
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
        List<ActivityData> criticalEncounters = zone.GetCriticalEncounterData()
            .OrderBy(ce => ce.Id)
            .ToList();

        if (criticalEncounters.Count == 0)
        {
            string emptyKey = prop.GetFieldLabelKey(owner).Replace(".label", ".empty", StringComparison.Ordinal);
            ui.Text(translator.T(emptyKey));
            return false;
        }

        bool changed = false;
        ExcelSheet<XIVDynamicEvent> sheet = data.GetExcelSheet<XIVDynamicEvent>();

        foreach (ActivityData criticalEncounter in criticalEncounters)
        {
            uint id = (uint)criticalEncounter.Id;
            string name = sheet.GetRow(id).Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Critical Encounter #{id}";
            }

            bool enabled = !disabled.Contains(id);

            ImGui.PushID(criticalEncounter.Id);
            if (ImGui.Checkbox($"{name} (#{criticalEncounter.Id})", ref enabled))
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
