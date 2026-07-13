using BOCCHI.Common.Data.CriticalEncounters;
using BOCCHI.Common.Data.Fates;

using BOCCHI.Automator.Data;

namespace BOCCHI.Automator.Services;

public interface IAutomator
{
    bool Enabled { get; }

    AutomatorState? CurrentState { get; }

    void Toggle();

    void Render();
}
