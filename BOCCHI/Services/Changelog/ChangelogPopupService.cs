using Dalamud.Plugin.Services;
using Ocelot.Config;
using Ocelot.Lifecycle;
using BOCCHI.Config;

namespace BOCCHI.Services.Changelog;

public sealed class ChangelogPopupService
(
    Configuration config,
    IConfigSaver saver,
    IChangelogWindow changelogWindow,
    IFramework framework
) : IOnStart
{
    private bool handled;

    public void OnStart()
    {
        // Defer off StartHost — same rationale as MOTD (avoid load-time deadlock).
        framework.RunOnTick(TryShowOnce);
    }

    private void TryShowOnce()
    {
        if (handled)
        {
            return;
        }

        handled = true;

        string current = ChangelogText.CurrentPluginVersion;
        string lastSeen = config.LastSeenPluginVersion ?? string.Empty;

        if (string.IsNullOrWhiteSpace(lastSeen))
        {
            // First install / first run of this feature: remember silently, no popup.
            Remember(current);
            return;
        }

        if (string.Equals(lastSeen, current, StringComparison.Ordinal))
        {
            return;
        }

        if (!ChangelogText.TryGetSectionForVersion(current, out _))
        {
            // Update with empty stub notes — don't nag; advance last-seen.
            Remember(current);
            return;
        }

        changelogWindow.ShowForCurrentVersion();
    }

    private void Remember(string version)
    {
        if (config.LastSeenPluginVersion == version)
        {
            return;
        }

        config.LastSeenPluginVersion = version;
        saver.Save();
    }
}
