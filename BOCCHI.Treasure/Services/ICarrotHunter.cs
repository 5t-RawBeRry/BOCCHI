namespace BOCCHI.Treasure.Services;

public enum CarrotHuntPhase
{
    Idle = 0,
    Pathing = 1,
    UsingItem = 2,
    WaitingForBunny = 3,
    OpeningBunny = 4
}

public interface ICarrotHunter
{
    bool Running { get; }

    CarrotHuntPhase Phase { get; }

    TimeSpan Elapsed { get; }

    int FortuneCarrotsRemaining { get; }

    bool IsVnavAvailable { get; }

    bool IsVnavReady { get; }

    void Toggle();

    /// <summary>Manual Fortune Carrot use (for stuck / intervene).</summary>
    bool UseFortuneCarrot();
}
