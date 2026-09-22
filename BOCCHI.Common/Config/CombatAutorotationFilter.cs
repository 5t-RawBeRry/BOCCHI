using Ocelot.Config.Renderers.Enum;
using Ocelot.Services.PluginStatus;

namespace BOCCHI.Common.Config;

/// <summary>
///     Combat rotation dropdown: primary backends (Wrath, BossMod) stay listed; Reborn forks only
///     appear when that plugin is installed. The saved choice is never hidden.
///     <para>
///     Wrath and Rotation Solver drive the rotation, but movement/dodging is BOCCHI AI (BossMod
///     misc AI) — so those need BossMod or BossMod Reborn when you actually run them.
///     </para>
/// </summary>
public class CombatAutorotationFilter(IPluginStatus plugins, AutomatorConfig config)
    : IEnumFilter<CombatAutorotation>
{
    private const string BossModReborn = "BossModReborn";

    public bool Filter(CombatAutorotation value)
    {
        // Never hide the saved choice. The renderer falls back to the first entry when the current
        // value is absent, which would show someone a backend they did not pick as though they had.
        if (value == CombatAutorotation.None || value == config.CombatAutorotation)
        {
            return true;
        }

        return value switch
        {
            // Primary options — always listed (Dependencies shows Not installed if missing).
            CombatAutorotation.WrathCombo => true,
            CombatAutorotation.BossMod => true,
            // Reborn forks — only if installed.
            CombatAutorotation.RotationSolverReborn =>
                plugins.IsInstalled(CombatPluginPresence.RotationSolver),
            CombatAutorotation.BossModReborn => plugins.IsInstalled(BossModReborn),
            _ => false,
        };
    }
}
