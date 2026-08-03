using System.Numerics;
using BOCCHI.Common.Data;
using BOCCHI.Common.Data.Traps;

namespace BOCCHI.Common.Data.Traps.SouthHorn;

public static partial class SouthHornTrapData
{
    private static List<TrapGroup> RightHallway { get; } =
    [
        // First room small traps
        new([
            new TrapDatum(new Vector3(726.4559f, -500.0001f, -56.77512f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(731.7543f, -500.0001f, -56.7751f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(744.4309f, -500.192f, -79.18898f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(744.4907f, -500.192f, -84.93067f), OccultObjectType.Trap),
        ]),

        // First room big traps
        new([
            new TrapDatum(new Vector3(732.09534f, -500.04004f, -76.629f), OccultObjectType.BigTrap),
            new TrapDatum(new Vector3(728.67035f, -500.04f, -79.736f), OccultObjectType.BigTrap),
            new TrapDatum(new Vector3(723.2473f, -500.085f, -72.448f), OccultObjectType.BigTrap),
        ]),

        // After first Geomancer room
        new([
            new TrapDatum(new Vector3(775.4798f, -492f, -5.562712f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(780.4813f, -492.00006f, -5.511192f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(802.2178f, -489.00006f, 4.564697f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(802.1575f, -489f, 8.566014f), OccultObjectType.Trap),
        ]),

        // In front of thief room
        new([
            new TrapDatum(new Vector3(818.8133f, -489f, -39.42315f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(816.4547f, -489f, -45.56033f), OccultObjectType.Trap),
        ]),


        // In front of mob guarded treasure room (one)
        new([
            new TrapDatum(new Vector3(773.6385f, -489f, -101.8446f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(770.0554f, -489f, -103.9902f), OccultObjectType.Trap),
        ]),

        // Before first add pack
        new([
            new TrapDatum(new Vector3(814.7891f, -489f, 51.77477f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(809.498f, -489f, 49.87538f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(804.2921f, -489f, 48.11743f), OccultObjectType.Trap),
        ]),

        // big traps in nooks
        new([
            new TrapDatum(new Vector3(811.6309f, -489.00003f, 75.44753f), OccultObjectType.BigTrap),
            new TrapDatum(new Vector3(809.176f, -489f, 78.81108f), OccultObjectType.BigTrap),
            new TrapDatum(new Vector3(768.9016f, -489f, 115.7147f), OccultObjectType.BigTrap),
            new TrapDatum(new Vector3(765.7593f, -489f, 117.5371f), OccultObjectType.BigTrap),
        ]),

        // Outside second treasure room (2)
        new([
            new TrapDatum(new Vector3(847.0126f, -489.00003f, 49.61197f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(848.8435f, -489f, 43.72131f), OccultObjectType.Trap),
        ]),

        // Around second Geomancer trap
        new([
            new TrapDatum(new Vector3(809.6052f, -489f, 93.29885f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(813.7463f, -489f, 98.21516f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(817.092f, -489f, 102.2518f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(784.2665f, -489f, 130.0462f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(780.5956f, -489.00003f, 125.7813f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(776.5737f, -489f, 120.8782f), OccultObjectType.Trap),
        ]),

        // Before button
        new([
            new TrapDatum(new Vector3(744.1068f, -489f, 119.3955f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(742.1111f, -489f, 114.2375f), OccultObjectType.Trap),
            new TrapDatum(new Vector3(740.2978f, -489f, 108.8097f), OccultObjectType.Trap),
        ]),
    ];
}


