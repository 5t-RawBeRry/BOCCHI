namespace BOCCHI.Common.Services;

public enum AutomationMode
{
    None = 0,
    IllegalMode = 1,
    PotsAndTreasure = 2,
    MobFarmer = 3,
    TreasureHunt = 4
}

/// <summary>Ensures only one primary automation mode runs at a time.</summary>
public interface IAutomationModeGuard
{
    /// <summary>Stop every other mode before starting <paramref name="mode"/>.</summary>
    void EnsureExclusive(AutomationMode mode);

    /// <summary>Resume Illegal Mode after a standalone treasure hunt if it was soft-paused.</summary>
    void NotifyStandaloneTreasureHuntEnded();

    /// <summary>Stop all modes, buffs, pathfinding, and chains.</summary>
    void EmergencyStop();
}
