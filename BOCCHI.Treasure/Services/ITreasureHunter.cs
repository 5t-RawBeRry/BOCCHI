using BOCCHI.Treasure.Hunt;

namespace BOCCHI.Treasure.Services;

public interface ITreasureHunter
{
    /// <summary>Hunt session is active (running or paused).</summary>
    bool Running { get; }

    bool Paused { get; }

    int StepIndex { get; }

    int StepCount { get; }

    float StepDistance { get; }

    TimeSpan Elapsed { get; }

    bool IsVnavAvailable { get; }

    bool IsVnavReady { get; }

    void Toggle();

    void Pause();

    void Resume();

    HuntPathfinderStep? GetCurrentStep();
}
