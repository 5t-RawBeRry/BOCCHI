using Ocelot.Ipc.PandorasBox;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;

namespace BOCCHI.Treasure.Services;

/// <summary>
///     Disables Pandora's "Automatically Open Chests" while BOCCHI chest automation is active,
///     then restores the previous setting. Ref-counted for overlapping hunt / pot farm.
/// </summary>
public sealed class PandoraAutoOpenHold(IPandorasBoxIpc pandora, ILogger<PandoraAutoOpenHold> log)
    : IOnStop
{
    /// <summary>Pandora class name (internal IPC), not the display name.</summary>
    public const string AutoOpenChestsInternalName = "AutoOpenChests";

    private int holds;

    private bool? wasEnabled;

    public void Hold()
    {
        if (holds++ > 0)
        {
            return;
        }

        if (!pandora.IsAvailable)
        {
            return;
        }

        wasEnabled = pandora.GetFeatureEnabledInternal(AutoOpenChestsInternalName);
        if (wasEnabled != true)
        {
            return;
        }

        pandora.SetFeatureEnabledInternal(AutoOpenChestsInternalName, false);
        log.Info("Paused Pandora AutoOpenChests while BOCCHI opens coffers");
    }

    public void Release()
    {
        if (holds <= 0)
        {
            return;
        }

        if (--holds > 0)
        {
            return;
        }

        if (wasEnabled == true && pandora.IsAvailable)
        {
            pandora.SetFeatureEnabledInternal(AutoOpenChestsInternalName, true);
            log.Info("Restored Pandora AutoOpenChests");
        }

        wasEnabled = null;
    }

    public void OnStop()
    {
        while (holds > 0)
        {
            Release();
        }
    }
}
