using BOCCHI.Common.Data.Traps;

namespace BOCCHI.Common.Data.Traps.SouthHorn;

public static partial class SouthHornTrapData
{
    public static IReadOnlyList<TrapGroup> Groups { get; } =
    [
        ..LeftHallway,
        ..RightHallway,
        ..HallwayJoin,
        ..LeftBridge,
        ..RightBridge,
        ..PuzzleRoom,
        ..FinalArea,
    ];
}
