using System.Numerics;
using BOCCHI.Common.Services.Logging;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Ocelot.Services.Translation;
using Ocelot.Windows;

namespace BOCCHI.Services.Logging;

public interface ILogsWindow : Ocelot.Windows.IWindow;

public sealed class LogsWindow : OcelotWindow, ILogsWindow
{
    private readonly BocchiLogsPanel panel;

    private readonly ITranslator<LogsWindow> translator;

    public LogsWindow(
        BocchiLogsPanel panel,
        ITranslator<LogsWindow> translator)
        : base("BOCCHI — Logs###bocchi.logs")
    {
        this.panel = panel;
        this.translator = translator;

        Size = new Vector2(720, 480);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 280),
            MaximumSize = new Vector2(1400, 1000),
        };

        translator.LanguageChanged += UpdateWindowTitle;
        translator.TranslationsChanged += UpdateWindowTitle;
        UpdateWindowTitle();
    }

    protected override void Render()
    {
        panel.Draw(translator, idSuffix: "_window");
    }

    private void UpdateWindowTitle()
    {
        WindowName = translator.T(".title") + "###bocchi.logs";
    }
}
