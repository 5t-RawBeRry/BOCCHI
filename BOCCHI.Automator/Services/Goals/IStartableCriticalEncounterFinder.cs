using BOCCHI.Common.Data.CriticalEncounters;

namespace BOCCHI.Automator.Services.Goals;

public interface IStartableCriticalEncounterFinder
{
    /// <summary>Register/Warmup CE Illegal Mode may start now.</summary>
    CriticalEncounter? FindStartable();
}
