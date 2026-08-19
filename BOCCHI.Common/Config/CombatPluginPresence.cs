using Ocelot.Ipc.RotationSolverReborn;
using Ocelot.Services.PluginStatus;

namespace BOCCHI.Common.Config;

/// <summary>Whether a combat backend is actually reachable this session.</summary>
public static class CombatPluginPresence
{
    /// <summary>Dalamud InternalName is still "RotationSolver"; only the display name gained "Reborn".</summary>
    public const string RotationSolver = "RotationSolver";

    public static bool RotationSolverReborn(IPluginStatus plugins, IRotationSolverRebornIpc rsr) =>
        plugins.IsLoaded(RotationSolver) || rsr.IsAvailable;
}
