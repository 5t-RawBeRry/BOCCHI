using BOCCHI.Common.Config;
using Ocelot.Rotation.Services;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Owns the ephemeral <c>BOCCHI AI</c> BossMod preset while Illegal Mode is on
///     when <see cref="AutomatorConfig.ToggleAiProvider"/> is set.
/// </summary>
public class AutoRotationController(IRotationService rotations, AutomatorConfig config)
{
    /// <summary>Illegal Mode started — create the preset (not yet active).</summary>
    public void PrepareForIllegalMode()
    {
        if (config.ToggleAiProvider)
        {
            rotations.EnsureAutoRotationPreset();
        }
    }

    /// <summary>Illegal Mode stopped — clear active and delete the preset.</summary>
    public void TeardownForIllegalMode()
    {
        if (config.ToggleAiProvider)
        {
            rotations.DestroyAutoRotationPreset();
        }
    }

    public void EnableForActivity()
    {
        if (config.ToggleAiProvider)
        {
            rotations.EnableAutoRotation();
        }
    }

    public void DisableForTravel()
    {
        if (config.ToggleAiProvider)
        {
            rotations.DisableAutoRotation();
        }
    }
}
