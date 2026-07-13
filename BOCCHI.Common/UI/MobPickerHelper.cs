using BOCCHI.Common.Data.Mobs;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Ocelot.Services.UI;

namespace BOCCHI.Common.UI;

public static class MobPickerHelper
{
    public static bool Draw(
        List<Mob> selectedMobs,
        IDataManager data,
        IUIService ui,
        ref string search,
        string searchHint,
        string selectedLabel,
        string listId,
        float listHeight = ImGuiSectionHelper.DefaultListHeight
    )
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##mob_search", searchHint, ref search, 128);

        var searchFilter = search;
        var selectableMobs = MobData.GetSelectableMobs()
            .Where(m => MobData.MatchesSearch(m, searchFilter, data))
            .ToList();

        ui.LabelledValue(selectedLabel, $"{selectedMobs.Count} / {selectableMobs.Count} shown");

        using var list = ImGuiSectionHelper.BoundedList(listId, listHeight);
        if (!list.IsOpen)
        {
            return false;
        }

        var changed = false;
        foreach (var mob in selectableMobs)
        {
            var isSelected = selectedMobs.Contains(mob);
            if (!ImGui.Checkbox($"{MobData.GetDisplayName(mob, data)}###mob_{(uint)mob}", ref isSelected))
            {
                continue;
            }

            changed = true;
            if (isSelected)
            {
                if (!selectedMobs.Contains(mob))
                {
                    selectedMobs.Add(mob);
                }
            }
            else
            {
                selectedMobs.Remove(mob);
            }
        }

        return changed;
    }
}
