using Ocelot.Config.Renderers.Enum;

namespace BOCCHI.Common.Config;

/// <summary>Illegal Mode combat automation choice.</summary>
public enum CombatAutorotation
{
    None = 0,
    WrathCombo = 1,
    RotationSolverReborn = 2,
    BossMod = 3,
    BossModReborn = 4,
}

public class CombatAutorotationDisplay : IEnumDisplay<CombatAutorotation>
{
    public string Display(CombatAutorotation value) => value switch
    {
        CombatAutorotation.WrathCombo => "Wrath Combo + BOCCHI AI",
        CombatAutorotation.RotationSolverReborn => "Rotation Solver + BOCCHI AI",
        CombatAutorotation.BossMod => "BossMod autorotation",
        CombatAutorotation.BossModReborn => "BossMod Reborn autorotation",
        _ => "None",
    };
}

public static class CombatAutorotationExtensions
{
    public static bool UsesCombatAutomation(this CombatAutorotation value) =>
        value != CombatAutorotation.None;
}
