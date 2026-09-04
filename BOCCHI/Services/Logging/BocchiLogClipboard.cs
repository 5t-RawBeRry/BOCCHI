using BOCCHI.Common;
using BOCCHI.Common.Config;
using BOCCHI.Common.Services.Logging;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace BOCCHI.Services.Logging;

public sealed class BocchiLogClipboard
(
    IBocchiLogBuffer buffer,
    IBocchiLogDiagnostics diagnostics,
    IChatGui chat,
    UIConfig uiConfig
) : IBocchiLogClipboard
{
    public void CopyAll(bool announceInChat = false)
    {
        string text = buffer.FormatAllForClipboard(diagnostics.BuildHeader());
        ImGui.SetClipboardText(text);
        if (announceInChat)
        {
            BocchiChat.Print(chat, uiConfig, "Copied all BOCCHI logs to clipboard.");
        }
    }
}
