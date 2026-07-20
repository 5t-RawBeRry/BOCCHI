namespace BOCCHI.Treasure.Services;

public interface ITreasureTracker
{
    IReadOnlyList<TreasureCoffer> Treasures { get; }

    bool CountInitialised { get; }

    int BronzeChests { get; }

    int SilverChests { get; }
}
