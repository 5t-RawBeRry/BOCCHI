using BOCCHI.Automator.Data;
namespace BOCCHI.Automator.Services;

public interface IAutomator
{
    bool Enabled { get; }

    AutomatorState? CurrentState { get; }

    void Toggle();

    /// <summary>Drop the current route and replan from the player's position (keeps the goal).</summary>
    void RefreshPathfinding();

    void Render();
}
