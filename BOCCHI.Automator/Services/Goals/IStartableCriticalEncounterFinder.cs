using BOCCHI.Common.Data.CriticalEncounters;

namespace BOCCHI.Automator.Services.Goals;

public interface IStartableCriticalEncounterFinder
{
    /// <summary>
    ///     Register/Warmup CE that Illegal Mode may start now (enabled, pot cutoff, completionist).
    /// </summary>
    CriticalEncounter? FindStartable();
}
