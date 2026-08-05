using BOCCHI.Treasure.Hunt;
using System.Numerics;

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

    /// <summary>Layout node ID of the last coffer step that was completed this session.</summary>
    uint? LastCheckedNodeId { get; }

    /// <summary>True while Pots &amp; Treasure owns this hunt session (hide standalone Start Hunt).</summary>
    bool ManagedByPotsTreasure { get; set; }

    /// <summary>True while Illegal Mode auto-filler owns this hunt session.</summary>
    bool ManagedByIllegalModeFiller { get; set; }

    bool IsVnavAvailable { get; }

    bool IsVnavReady { get; }

    void Toggle();

    void Pause();

    void Resume();

    HuntPathfinderStep? GetCurrentStep();

    /// <summary>Next WalkToNode coffer from the current step (where the hunt continues).</summary>
    bool TryGetResumeCoffer(out uint nodeId, out Vector3 position);

    /// <summary>Place the in-game map flag on the resume coffer position.</summary>
    bool FlagResumePoint();
}
