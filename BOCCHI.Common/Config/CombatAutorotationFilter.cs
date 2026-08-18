using Ocelot.Config.Renderers.Enum;
using Ocelot.Services.PluginStatus;

namespace BOCCHI.Common.Config;

/// <summary>
///     Hides combat backends whose plugins are not loaded, so the dropdown only offers choices that
///     would actually work.
///     <para>
///     Wrath and Rotation Solver drive the rotation, but the movement and dodging half is BOCCHI AI,
///     which is BossMod's misc AI — so those two additionally need BossMod or BossMod Reborn. That
///     is why the dependency page marks all three as in use when either is selected.
///     </para>
/// </summary>
public class CombatAutorotationFilter(IPluginStatus plugins, AutomatorConfig config)
    : IEnumFilter<CombatAutorotation>
{
    private const string WrathCombo = "WrathCombo";

    /// <summary>Dalamud InternalName is still "RotationSolver"; only the display name gained "Reborn".</summary>
    private const string RotationSolver = "RotationSolver";

    private const string BossMod = "BossMod";

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
            CombatAutorotation.WrathCombo => plugins.IsLoaded(WrathCombo) && HasBocchiAi(),
            CombatAutorotation.RotationSolverReborn => plugins.IsLoaded(RotationSolver) && HasBocchiAi(),
            CombatAutorotation.BossMod => plugins.IsLoaded(BossMod),
            CombatAutorotation.BossModReborn => plugins.IsLoaded(BossModReborn),
            _ => false,
        };
    }

    /// <summary>BOCCHI AI handles movement/dodging for the Wrath and RSR options.</summary>
    private bool HasBocchiAi() => plugins.IsLoaded(BossMod) || plugins.IsLoaded(BossModReborn);
}
