using System.Numerics;
using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Traps;

namespace BOCCHI.Common.Data.Traps.SouthHorn;

public static partial class SouthHornTrapData
{
    private static List<TrapGroup> HallwayJoin { get; } =
    [
        // Before bridge
        new([
            new TrapDatum(new Vector3(703.45f, -504f, 99.93414f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(700.0876f, -504f, 99.94228f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(697.0103f, -504.00012f, 99.96162f), OccultObjectType.Trap),
        ]),

        // Before stairs
        new([
            new TrapDatum(new Vector3(703.0069f, -504f, 79.00574f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(700.0394f, -504f, 79.00847f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(696.9874f, -504f, 78.96864f), OccultObjectType.Trap),
        ]),
    ];
}


