using BOCCHI.Common.Data.Traps;

namespace BOCCHI.Common.Data.Traps.SouthHorn;

public static partial class SouthHornTrapData
{
    private static IReadOnlyList<TrapGroup>? groups;

    public static IReadOnlyList<TrapGroup> Groups =>
        groups ??=
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
