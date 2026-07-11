using BOCCHI.Treasure.Hunt;

namespace BOCCHI.Treasure.Services;

public interface ITreasureHunter
{
    bool Running { get; }

    int StepIndex { get; }

    int StepCount { get; }

    float StepDistance { get; }

    TimeSpan Elapsed { get; }

    bool IsVnavReady { get; }

    void Toggle();

    HuntPathfinderStep? GetCurrentStep();
}
