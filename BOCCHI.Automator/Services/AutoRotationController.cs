using BOCCHI.Common.Config;
using Ocelot.Rotation.Services;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Turns autorotation off in transit and on for FATE/CE when
///     <see cref="AutomatorConfig.ToggleAiProvider"/> is set.
///     BossMod / BossMod Reborn use the built-in <c>VBM AI</c> preset
///     (not legacy <c>/vbmai</c> / <c>/bmrai</c>).
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
