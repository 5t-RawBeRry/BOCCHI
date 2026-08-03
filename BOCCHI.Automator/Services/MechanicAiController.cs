using BOCCHI.Common.Config;
using Ocelot.Mechanic.Services;

namespace BOCCHI.Automator.Services;

/// <summary>
///     Turns mechanic AI off in transit and on for FATE/CE when <see cref="AutomatorConfig.ToggleAiProvider"/> is set.
/// </summary>
public class MechanicAiController(IMechanicService mechanics, AutomatorConfig config)
{
    public void EnableForActivity()
    {
        if (config.ToggleAiProvider)
        {
            mechanics.Enable();
        }
    }

    public void DisableForTravel()
    {
        if (config.ToggleAiProvider)
        {
            mechanics.Disable();
        }
    }
}
