using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Mobs;
using BOCCHI.MobFarmer.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Ocelot.Config;
using Ocelot.Services.UI;

namespace BOCCHI.MobFarmer;

public class MobFarmerRenderer(
    IMobFarmer farmer,
    IMobScanner scanner,
    MobFarmerConfig config,
    IConfigSaver saver,
    IDataManager data,
    IUIService ui
) : IDynamicRenderer
{
    private string mobSearch = string.Empty;

    public uint Order => 40;

    public void Render()
    {
        ui.Text("Mob Farmer");
        ImGui.Indent();

        if (ImGui.Button(farmer.Running ? "Stop" : "Start"))
        {
            farmer.Toggle();
        }

        if (farmer.Running)
        {
            ui.LabelledValue("Phase", farmer.Phase);
        }

        ui.LabelledValue("Not engaged", scanner.NotInCombat.Count());
        ui.LabelledValue("Engaged", scanner.InCombat.Count());

        DrawMobPicker();

        if (farmer.Running)
        {
            farmer.Render();
        }

        ImGui.Unindent();
    }

    public bool ShouldRender()
    {
        return true;
    }

    private void DrawMobPicker()
    {
        ImGui.Separator();
        ui.Text("Mobs");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##mob_search", "Search name or ID...", ref mobSearch, 128);

        var changed = false;
        foreach (var mob in MobData.GetSelectableMobs().Where(m => MobData.MatchesSearch(m, mobSearch, data)))
        {
            var selected = config.Mobs.Contains(mob);
            if (ImGui.Checkbox($"{MobData.GetDisplayName(mob, data)}###mob_{(uint)mob}", ref selected))
            {
                changed = true;
                if (selected)
                {
                    if (!config.Mobs.Contains(mob))
                    {
                        config.Mobs.Add(mob);
                    }
                }
                else
                {
                    config.Mobs.Remove(mob);
                }
            }
        }

        if (changed)
        {
            saver.Save();
        }
    }
}
