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
    Func<IMobFarmer> farmerFactory,
    IMobScanner scanner,
    MobFarmerConfig config,
    IConfigSaver saver,
    IDataManager data,
    IUIService ui
) : IDynamicRenderer
{
    private IMobFarmer? farmer;

    private IMobFarmer Farmer => farmer ??= farmerFactory();

    private string mobSearch = string.Empty;

    public uint Order => 40;

    public void Render()
    {
        ui.Text("Mob Farmer");
        ImGui.Indent();

        if (ImGui.Button(Farmer.Running ? "Stop" : "Start"))
        {
            Farmer.Toggle();
        }

        if (Farmer.Running)
        {
            ui.LabelledValue("Phase", Farmer.Phase);
        }

        ui.LabelledValue("Not engaged", scanner.NotInCombat.Count());
        ui.LabelledValue("Engaged", scanner.InCombat.Count());

        DrawMobPicker();

        if (Farmer.Running)
        {
            Farmer.Render();
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
