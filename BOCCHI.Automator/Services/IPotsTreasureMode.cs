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

    bool Paused { get; }

    PotsTreasurePhase Phase { get; }

    void Toggle();

    void Pause();

    void Resume();

    void ResumeTreasureHunt();

    bool ManagedByMobFarmer { get; }

    /// <summary>
    ///     Run one pot window (and chests) without tearing down Mob Farmer. No treasure hunt.
    ///     Returns false if vnav is missing.
    /// </summary>
    bool StartManagedFromFarmer();

    void StopManagedFromFarmer();
}
