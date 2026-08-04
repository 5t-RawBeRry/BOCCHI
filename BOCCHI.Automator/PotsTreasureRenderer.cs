using BOCCHI.Automator.Services;
using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Treasure;
using BOCCHI.Treasure.Services;
using Dalamud.Bindings.ImGui;
using Ocelot.Extensions;
using Ocelot.Services.Translation;
using Ocelot.Services.UI;
using Ocelot.Windows;

namespace BOCCHI.Automator;

public class PotsTreasureRenderer
(
    Func<IPotsTreasureMode> potsTreasureFactory,
    ITreasureHunter hunter,
    UIConfig uiConfig,
    IUIService ui,
    ITranslator<MainWindow> translator
) : IDynamicRenderer
{
    private IPotsTreasureMode? potsTreasure;

    private IPotsTreasureMode PotsTreasure => potsTreasure ??= potsTreasureFactory();

    public MainWindowSection Section => MainWindowSection.PotsTreasure;

    public void Render()
    {
        if (ImGui.Button(PotsTreasure.Running
            ? translator.T(".automation.pots_treasure.stop")
            : translator.T(".automation.pots_treasure.start")))
        {
            PotsTreasure.Toggle();
        }

        ImGui.TextWrapped(translator.T(".automation.pots_treasure.description"));

        if (!PotsTreasure.Running)
        {
            return;
        }

        ui.LabelledValue(
            translator.T(".automation.pots_treasure.phase"),
            translator.T($".automation.pots_treasure.phases.{PotsTreasure.Phase.ToString().ToSnakeCase()}"));

        if (hunter.Elapsed > TimeSpan.Zero)
        {
            ui.LabelledValue(translator.T(".treasure.elapsed"), $"{hunter.Elapsed:mm\\:ss}");
        }

        TreasureHuntStatusUi.DrawProgress(hunter, ui, translator);
    }

    public bool ShouldRender() => uiConfig.ShowPotsTreasureSection;
}
