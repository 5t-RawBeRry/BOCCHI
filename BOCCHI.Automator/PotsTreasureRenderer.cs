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
    TreasureConfig treasureConfig,
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
        ImGui.Spacing();

        if (ImGui.Button(PotsTreasure.Running
            ? translator.T(".automation.pots_treasure.stop")
            : translator.T(".automation.pots_treasure.start")))
        {
            PotsTreasure.Toggle();
        }

        ImGui.Spacing();
        ImGui.TextWrapped(translator.T(".automation.pots_treasure.description"));

        if (!PotsTreasure.Running)
        {
            return;
        }

        ImGui.Spacing();
        ui.LabelledValue(
            translator.T(".automation.pots_treasure.phase"),
            translator.T($".automation.pots_treasure.phases.{PotsTreasure.Phase.ToString().ToSnakeCase()}"));

        // Compact hunt status while this mode owns the treasure hunter.
        if (hunter.ManagedByPotsTreasure && (hunter.Running || hunter.Elapsed > TimeSpan.Zero))
        {
            ImGui.Spacing();
            if (hunter.Elapsed > TimeSpan.Zero)
            {
                ui.LabelledValue(translator.T(".treasure.elapsed"), $"{hunter.Elapsed:mm\\:ss}");
            }

            TreasureHuntStatusUi.DrawProgress(hunter, ui, translator, treasureConfig);
        }
    }

    public bool ShouldRender() => uiConfig.ShowPotsTreasureSection;
}
