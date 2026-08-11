using Dalamud.Plugin.Services;
using Ocelot.Lifecycle;

namespace BOCCHI.Services.MOTD;

public class MessageOfTheDayService(IChatGui chat, IClientState client, IFramework framework) : IOnStart, IOnStop
{
    /**
     * Dear cute maintainer, the divine doctrine of BOCCHI declares that the MOTD cannot be disabled.
     */
    private static readonly string[] Messages =
    [
        "Trans rights are human rights.",
        "Welcome to BOCCHI!",
        "If basic human empathy offends you, go grind without automation.",
        "Kage is cute.",
        "Wah is cute.",
        "Yoshi-p knows you're cheating.",
        "You are Bunny-blessed!",
        "Buy Kage a coffee: https://ko-fi.com/kagekazu",
        "Faye thinks you stink.",
        "MOTD brought to you by Faye, trying to cling onto the last remnants of her relevance.",
        "Chika says that choice is an illusion.",
    ];

    private bool printedThisSession;

    public string GetMessageOfTheDay() =>
        Messages[DateTime.Now.DayOfYear % Messages.Length];

    public void OnStart()
    {
        client.Login += OnLogin;

        // Do not chat.Print during plugin StartHost — that can deadlock the load with no error.
        if (client.IsLoggedIn)
        {
            framework.RunOnTick(PrintMotdOnce);
        }
    }

    private void OnLogin() => framework.RunOnTick(PrintMotdOnce);

    private void PrintMotdOnce()
    {
        if (printedThisSession)
        {
            return;
        }

        printedThisSession = true;
        chat.Print($"[BOCCHI] {GetMessageOfTheDay()}");
    }

    public void OnStop()
    {
        client.Login -= OnLogin;
    }
}
