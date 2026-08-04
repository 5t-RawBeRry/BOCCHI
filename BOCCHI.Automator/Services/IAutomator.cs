using BOCCHI.Automator.Data;

namespace BOCCHI.Automator.Services;

public interface IAutomator
{
    /// <summary>Illegal Mode is on (not Pots &amp; Treasure).</summary>
    bool Enabled { get; }

    /// <summary>Automator pipeline is active for Illegal Mode or Pots &amp; Treasure.</summary>
    bool IsActive { get; }

    bool IsPotsAndTreasure { get; }

    /// <summary>Treasure hunt owns vnav (Illegal Mode soft-pause or Pots &amp; Treasure filler).</summary>
    bool SuspendedForTreasure { get; }

    /// <summary>Illegal Mode is on (including while suspended for treasure).</summary>
    bool IsIllegalMode { get; }

    /// <summary>Suspend or resume the automator pipeline so treasure hunt can own vnav.</summary>
    void SetSuspendedForTreasure(bool suspended);

    AutomatorState? CurrentState { get; }

    void Toggle();

    void TogglePotsAndTreasure();

    /// <summary>Drop the current route and replan from the player's position (keeps the goal).</summary>
    void RefreshPathfinding();

    void Render();
}
