namespace BOCCHI.Treasure.Services;

public interface ITreasureTracker
{
    IReadOnlyList<TreasureCoffer> Treasures { get; }

    bool CountInitialised { get; }

    /// <summary>UTC time of the last successful Treasure Sight WideText parse.</summary>
    DateTime LastCountUpdateUtc { get; }

    int BronzeChests { get; }

    int SilverChests { get; }
}
