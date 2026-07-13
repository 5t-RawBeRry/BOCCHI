using System.Reflection;
using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Data.Zones;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Ocelot.Config.Fields;
using Ocelot.Config.Renderers;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using XIVFate = Lumina.Excel.Sheets.Fate;

namespace BOCCHI.Common.Config.Fields
{
    public sealed class DisabledFateIdsAttribute()
        : UIFieldAttribute(typeof(Renderers.DisabledFateIdsRenderer));
}

namespace BOCCHI.Common.Config.Renderers
{
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

            var disabled = (HashSet<uint>?)prop.GetValue(target) ?? [];
            if (prop.GetValue(target) == null)
            {
                prop.SetValue(target, disabled);
            }

            prop.Label(owner, translator);
            prop.Tooltip(owner, translator);

            var zone = zones.GetZone();
            var fates = zone.GetNormalFateData()
                .Concat(zone.GetPotFateData())
                .OrderBy(f => f.Id)
                .ToList();

            if (fates.Count == 0)
            {
                var emptyKey = prop.GetFieldLabelKey(owner).Replace(".label", ".empty", StringComparison.Ordinal);
                ui.Text(translator.T(emptyKey));
                return false;
            }

            var changed = false;
            var fateSheet = data.GetExcelSheet<XIVFate>();

            foreach (var fate in fates)
            {
                var fateId = (uint)fate.Id;
                var name = fateSheet.GetRow(fateId).Name.ToString();
                var enabled = !disabled.Contains(fateId);

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
}
