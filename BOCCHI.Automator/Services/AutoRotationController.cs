using BOCCHI.Common.Config;
using Ocelot.Rotation.Services;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Toggles the <c>BOCCHI AI</c> BossMod preset while traveling / at FATE-CE
///     when <see cref="AutomatorConfig.ToggleAiProvider"/> is set.
/// </summary>
public class AutoRotationController(IRotationService rotations, AutomatorConfig config)
{
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
