namespace BOCCHI.Automator.Services;

public enum PotsTreasurePhase
{
    Off,
    Hunting,
    DoingPots
}

public interface IPotsTreasureMode : Ocelot.Lifecycle.IOnUpdate
{
    bool Running { get; }

    PotsTreasurePhase Phase { get; }

    void Toggle();

    void ResumeTreasureHunt();
}
