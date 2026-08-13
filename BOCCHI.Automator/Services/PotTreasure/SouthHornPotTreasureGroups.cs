// Authored South Horn pot-chest directional groups (treasureCofferGroups).
using BOCCHI.Common.Data.Zones;
using System.Numerics;

namespace BOCCHI.Automator.Services.PotTreasure;

public static class SouthHornPotTreasureGroups
{
    // fateId -> groupKey -> candidates
    public static IReadOnlyDictionary<int, IReadOnlyDictionary<string, IReadOnlyList<PotTreasureCandidate>>> ByFate { get; } =
        new Dictionary<int, IReadOnlyDictionary<string, IReadOnlyList<PotTreasureCandidate>>>
        {
            [1976] = new Dictionary<string, IReadOnlyList<PotTreasureCandidate>>
            {
                ["north"] =
                [
                    new("N1", new Vector3(330.866f, 6.717f, -654.534f), 6),
                ],
                ["northeast"] =
                [
                    new("NE1", new Vector3(587.704f, 78.896f, -545.817f), 2),
                    new("NE2", new Vector3(571.5841f, 51.451305f, -813.1642f), 1),
                ],
                ["east"] =
                [
                    new("E1", new Vector3(803.661f, 96f, -354.181f), 4),
                    new("E2", new Vector3(684.4223f, 96.10129f, -165.4811f), 4),
                    new("E3", new Vector3(878.113f, 108.29f, -91.106f), 5),
                    new("E4", new Vector3(891.2597f, 120f, -20.672f), 5),
                ],
                ["southeast"] =
                [
                    new("SE1", new Vector3(606.4641f, 108.07402f, 184.8517f), 4),
                    new("SE2", new Vector3(662.439f, 120f, 161.134f), 21),
                    new("SE3", new Vector3(570.2421f, 64.66202f, 272.1734f), 14),
                    new("SE4", new Vector3(705.2716f, 68.143616f, 358.6714f), 14),
                ],
                ["south"] =
                [
                    new("S1", new Vector3(341.4413f, 95.99999f, 194.7507f), 4),
                    new("S2", new Vector3(263.256f, 100.385f, 326.683f), 10),
                    new("S3", new Vector3(80.19762f, 101.27949f, 391.2263f), 10),
                    new("S4", new Vector3(224.7233f, 68.7328f, 518.668f), 15),
                    // Bears south from the pot center (200, -215) — was filed under southwest,
                    // so a "south" hint never offered it.
                    new("S5", new Vector3(-54.69518f, 99.40573f, 405.0261f), 10),
                ],
                ["southwest"] =
                [
                    new("SW1", new Vector3(-165.2374f, 95.33837f, 437.4505f), 10),
                    new("SW2", new Vector3(-324.2736f, 121f, 203.2017f), 11),
                    new("SW3", new Vector3(-313.2906f, 108.10962f, 70.76207f), 12),
                ],
                ["west"] =
                [
                    new("W1", new Vector3(-459.1735f, 93.57443f, 5.054043f), 12),
                    new("W2", new Vector3(-312.2778f, 103.19944f, -35.25348f), 11),
                    new("W3", new Vector3(-476.3011f, 101.44228f, -86.69939f), 11),
                    new("W4", new Vector3(-660.5336f, 98f, -216.7666f), 11),
                    new("W5", new Vector3(-382.44f, 109.302f, -378.348f), 11),
                ],
                ["northwest"] =
                [
                    new("NW1", new Vector3(19.74f, 26.046f, -420.977f), 6),
                    new("NW2", new Vector3(-216.372f, 5.44694f, -510.1361f), 6),
                    new("NW3", new Vector3(-386.5904f, -0.139941f, -461.0976f), 6),
                    new("NW4", new Vector3(-534.6993f, 2.999998f, -651.6244f), 8),
                    new("NW5", new Vector3(-333.3444f, 3f, -861.1722f), 8),
                    new("NW6", new Vector3(-188.1745f, 2.999999f, -717.2005f), 7),
                ],
            },
            [1977] = new Dictionary<string, IReadOnlyList<PotTreasureCandidate>>
            {
                ["north"] =
                [
                    new("N1", new Vector3(-195.442f, 110.153f, -287.891f), 11),
                    new("N2", new Vector3(-386.437f, 98.60658f, -221.7847f), 11),
                    new("N3", new Vector3(-554.615f, 99.018f, -309.123f), 11),
                    new("N4", new Vector3(-676.62f, 128.574f, 1.532f), 13),
                    new("N5", new Vector3(-645.3027f, 135.69208f, -73.54771f), 13),
                    new("N6", new Vector3(-730.5441f, 107.694275f, -371.4776f), 25),
                ],
                ["northeast"] =
                [
                    new("NE1", new Vector3(74.73397f, 110.494316f, -394.1289f), 9),
                    new("NE2", new Vector3(69.70596f, 111.56108f, -239.064f), 9),
                    new("NE3", new Vector3(-38.97946f, 102.073296f, -175.4589f), 9),
                    new("NE4", new Vector3(393.019f, 104f, -124.165f), 9),
                    new("NE5", new Vector3(301.8741f, 103.784424f, 70.59854f), 9),
                    new("NE6", new Vector3(107.0611f, 105.699875f, 146.7059f), 10),
                ],
                ["east"] =
                [
                    new("E1", new Vector3(17.60418f, 65.93209f, 674.6207f), 15),
                    new("E2", new Vector3(67.45271f, 69.477974f, 745.8658f), 15),
                    new("E3", new Vector3(200.1241f, 56f, 624.2285f), 15),
                    new("E4", new Vector3(393.2685f, 57.545956f, 844.6924f), 17),
                    new("E5", new Vector3(440.836f, 70.3f, 876.41f), 17),
                    new("E6", new Vector3(825.9521f, 70f, 772.4054f), 17),
                    new("E7", new Vector3(781.251f, 70f, 560.07f), 17),
                    new("E8", new Vector3(423.3505f, 70.3f, 578.9013f), 17),
                ],
                ["southeast"] =
                [
                    new("SE1", new Vector3(-60.72729f, 69.687035f, 828.4997f), 14),
                ],
                ["south"] =
                [
                    new("S1", new Vector3(-603.3457f, 139f, 858.6771f), 27),
                ],
                ["southwest"] =
                [
                    new("SW1", new Vector3(-746.132f, 172f, 828.881f), 27),
                    new("SW2", new Vector3(-734.1434f, 170.99998f, 683.7238f), 28),
                    new("SW3", new Vector3(-713.6796f, 203f, 710.08f), 28),
                    new("SW4", new Vector3(-836.1612f, 107f, 770.2822f), 26),
                ],
                ["west"] =
                [
                    new("W1", new Vector3(-837.49f, 107f, 599.9f), 26),
                ],
                ["northwest"] =
                [
                    new("NW1", new Vector3(-811.84f, 114.07f, -225.39f), 25),
                    new("NW2", new Vector3(-798.7886f, 84.22545f, -4.822005f), 24),
                    new("NW3", new Vector3(-829.598f, 62.66814f, 66.82948f), 13),
                ],
            },
        };

    public static bool TryGetGroup(int fateId, string groupKey, out IReadOnlyList<PotTreasureCandidate> candidates)
    {
        candidates = Array.Empty<PotTreasureCandidate>();
        if (!ByFate.TryGetValue(fateId, out var groups) || !groups.TryGetValue(groupKey, out var list))
            return false;
        candidates = list;
        return list.Count > 0;
    }

    public static bool HasGroups(int fateId) => ByFate.ContainsKey(fateId);
}

