using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.MobFarmer.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.MobFarmer;

public class MobFarmerRenderer
(
    Func<IMobFarmer> farmerFactory,
    IMobScanner scanner,
    MobFarmerConfig config,
    UIConfig uiConfig,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    private IMobFarmer? farmer;

    private IMobFarmer Farmer => farmer ??= farmerFactory();

    public MainWindowSection Section => MainWindowSection.MobFarmer;

    public void Render()
    {
        if (ImGui.Button(Farmer.Running
            ? translator.T(".automation.mob_farmer.stop")
            : translator.T(".automation.mob_farmer.start")))
        {
            Farmer.Toggle();
        }

        ui.LabelledValue(translator.T(".automation.mob_farmer.not_engaged"), scanner.NotInCombat.Count());
        ui.LabelledValue(translator.T(".automation.mob_farmer.engaged"), scanner.InCombat.Count());
        ui.LabelledValue(translator.T(".automation.mob_farmer.selected_mobs"), config.Mobs.Count);
        ImGui.TextUnformatted(translator.T(".automation.mob_farmer.configure_mobs"));

        if (Farmer.Running)
        {
            Farmer.Render();
        }
    }

    public bool ShouldRender() => uiConfig.ShowMobFarmerSection;
}
