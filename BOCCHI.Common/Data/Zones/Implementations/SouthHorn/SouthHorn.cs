using System.Numerics;
using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Zones.Graph.Factory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;

namespace BOCCHI.Common.Data.Zones.Implementations.SouthHorn;

public class SouthHorn(
    IObjectTable objects,
    IDalamudPluginInterface plugin,
    IGraphFactory graphs,
    IPathfinder pathfinder,
    ILogger logger
) : BaseZone(objects, plugin, graphs, pathfinder, logger, ZoneId.SouthHorn)
{
    protected override uint BasecampPlaceNameId { get; } = 4944;

    public override AethernetData GetMainAetheryte()
    {
        return BaseCamp;
    }

    public override Vector3 GetAetherytePosition()
    {
        return new Vector3(830.75f, 72.98f, -695.98f);
    }

    public override Vector3 GetStartingPosition()
    {
        return new Vector3(850.33f, 72.99f, -704.07f);
    }

    public override List<AethernetData> GetAetherytes()
    {
        return
        [
            BaseCamp,
            TheWanderersHaven,
            CrystallizedCaverns,
            Eldergrowth,
            Stonemarsh,
        ];
    }

    public override List<AethernetData> GetAethernetShards()
    {
        return
        [
            TheWanderersHaven,
            CrystallizedCaverns,
            Eldergrowth,
            Stonemarsh,
        ];
    }

    protected override ushort GetForkedTowerEventId()
    {
        return 48;
    }

    public override List<ActivityData> GetNormalFateData()
    {
        return
        [
            new ActivityData(1962, new Vector3(162f, 56f, 676f)), // "Rough Waters"
            new ActivityData(1963, new Vector3(373.20f, 70f, 486f)), // "The Golden Guardian"
            new ActivityData(1964, new Vector3(-226.10f, 116.38f, 254f)), // "King of the Crescent"
            new ActivityData(1965, new Vector3(-548.50f, 3f, -595f)), // "The Winged Terror"
            new ActivityData(1966, new Vector3(-223.10f, 107f, 36f)), // "An Unending Duty"
            new ActivityData(1967, new Vector3(-48.10f, 111.76f, -320f)), // "Brain Drain"
            new ActivityData(1968, new Vector3(-370f, 75f, 650f)), // "A Delicate Balance"
            new ActivityData(1969, new Vector3(-589.10f, 96.50f, 333f)), // "Sworn to Soil"
            new ActivityData(1970, new Vector3(-71f, 71.31f, 557f)), // "A Prying Eye"
            new ActivityData(1971, new Vector3(79f, 97.86f, 278f)), // "Fatal Allure"
            new ActivityData(1972, new Vector3(413f, 96f, -13f)), // "Serving Darkness"
        ];
    }

    public override List<ActivityData> GetPotFateData()
    {
        return
        [
            new ActivityData(1976, new Vector3(200f, 111.73f, -215f)), // "Persistent Pots" (North)
            new ActivityData(1977, new Vector3(-481f, 75f, 528f)), // "Pleading Pots" (South)
        ];
    }

    public override List<ActivityData> GetCriticalEncounterData()
    {
        return
        [
            new ActivityData(33, new Vector3(286.25f, 70f, 699.25f), 25f), // "Scourge of the Mind"
            new ActivityData(34, new Vector3(483.39f, 65f, 382.15f), 25f), // "The Black Regiment"
            new ActivityData(35, new Vector3(597.41f, 79f, 809.66f), 25f), // "The Unbridled"
            new ActivityData(36, new Vector3(681f, 74f, 512f), 25f), // "Crawling Death"
            new ActivityData(37, new Vector3(-332.32f, 75f, 799.95f), 25f), // "Calamity Bound"
            new ActivityData(38, new Vector3(-447.50f, 92f, 58f), 20f), // "Trial by Claw"
            new ActivityData(39, new Vector3(-800f, 44f, 245f), 20f), // "From Times Bygone"
            new ActivityData(40, new Vector3(680f, 96f, -256f), 25f), // "Company of Stone"
            new ActivityData(41, new Vector3(-128f, 1f, -850f), 15f), // "Shark Attack"
            new ActivityData(42, new Vector3(637.82f, 108f, -54.07f), 25f), // "On the Hunt"
            new ActivityData(43, new Vector3(-384f, 5f, -588.25f), 15f), // "With Extreme Prejudice"
            new ActivityData(44, new Vector3(492f, 96f, -389.75f), 25f), // "Noise Complaint"
            new ActivityData(45, new Vector3(96.25f, 20f, -512f), 25f), // "Cursed Concern"
            new ActivityData(46, new Vector3(844.55f, 122f, 154.45f), 20f), // "Eternal Watch"
            new ActivityData(47, new Vector3(-577f, 97f, -187.25f), 20f), // "Flame of Dusk"
        ];
    }

    public override List<TreasureData> GetTreasureData()
    {
        return
        [
            new TreasureData(1789, 5),
            new TreasureData(1790, 11),
            new TreasureData(1791, 13),
            new TreasureData(1792, 16),
            new TreasureData(1793, 14),
            new TreasureData(1794, 23),
            new TreasureData(1795, 25),
            new TreasureData(1796, 28),
            new TreasureData(1797, 1),
            new TreasureData(1798, 1),
            new TreasureData(1799, 2),
            new TreasureData(1800, 2),
            new TreasureData(1801, 3),
            new TreasureData(1802, 3),
            new TreasureData(1803, 4),
            new TreasureData(1804, 4),
            new TreasureData(1805, 5),
            new TreasureData(1806, 5),
            new TreasureData(1807, 3),
            new TreasureData(1808, 6),
            new TreasureData(1809, 6),
            new TreasureData(1810, 7),
            new TreasureData(1811, 8),
            new TreasureData(1812, 8),
            new TreasureData(1813, 9),
            new TreasureData(1814, 9),
            new TreasureData(1815, 10),
            new TreasureData(1816, 10),
            new TreasureData(1817, 11),
            new TreasureData(1818, 11),
            new TreasureData(1819, 12),
            new TreasureData(1820, 12),
            new TreasureData(1821, 13),
            new TreasureData(1822, 13),
            new TreasureData(1823, 14),
            new TreasureData(1824, 14),
            new TreasureData(1825, 15),
            new TreasureData(1826, 15),
            new TreasureData(1827, 16),
            new TreasureData(1828, 16),
            new TreasureData(1829, 17),
            new TreasureData(1830, 17),
            new TreasureData(1831, 18),
            new TreasureData(1832, 18),
            new TreasureData(1833, 19),
            new TreasureData(1834, 19),
            new TreasureData(1835, 20),
            new TreasureData(1836, 20),
            new TreasureData(1837, 21),
            new TreasureData(1838, 21),
            new TreasureData(1839, 22),
            new TreasureData(1840, 22),
            new TreasureData(1841, 22),
            new TreasureData(1842, 99), // 23 marked as 99 to avoid this chest, this is on a lip that vnav can't walk to
            new TreasureData(1843, 24),
            new TreasureData(1844, 24),
            new TreasureData(1845, 25),
            new TreasureData(1846, 25),
            new TreasureData(1847, 26),
            new TreasureData(1848, 26),
            new TreasureData(1849, 27),
            new TreasureData(1850, 27),
            new TreasureData(1851, 28),
            new TreasureData(1852, 28),
            new TreasureData(1853, 21),
            new TreasureData(1854, 10),
            new TreasureData(1855, 11),
            new TreasureData(1856, 11),
        ];
    }

    public override Dictionary<int, List<PotChestData>> GetPotChestData()
    {
        return new Dictionary<int, List<PotChestData>>
        {
            // North
            {
                1976, [
                    new PotChestData(new Vector3(571.5841f, 51.451305f, -813.1642f), 99),
                    new PotChestData(new Vector3(662.4388f, 120f, 161.1339f), 99),
                    new PotChestData(new Vector3(606.4641f, 108.07402f, 184.8517f), 99),
                    new PotChestData(new Vector3(-312.2778f, 103.19944f, -35.25348f), 99),
                    new PotChestData(new Vector3(587.7039f, 78.8956f, -545.8168f), 99),
                    new PotChestData(new Vector3(891.2597f, 120f, -20.672f), 99),
                    new PotChestData(new Vector3(878.1131f, 108.28959f, -91.1057f), 99),
                    new PotChestData(new Vector3(803.6609f, 95.99998f, -354.1809f), 99),
                    new PotChestData(new Vector3(341.4413f, 95.99999f, 194.7507f), 99),
                    new PotChestData(new Vector3(570.2421f, 64.66201f, 272.1734f), 99),
                    new PotChestData(new Vector3(-216.372f, 5.4469404f, -510.1361f), 99),
                    new PotChestData(new Vector3(684.4223f, 96.10129f, -165.4811f), 99),
                    new PotChestData(new Vector3(-188.1745f, 2.999999f, -717.2005f), 99),
                    new PotChestData(new Vector3(-476.3011f, 101.44228f, -86.69939f), 99),
                    new PotChestData(new Vector3(80.19762f, 101.27949f, 391.2263f), 99),
                    new PotChestData(new Vector3(-534.6993f, 2.999998f, -651.6244f), 99),
                    new PotChestData(new Vector3(-165.2374f, 95.33837f, 437.4505f), 99),
                    new PotChestData(new Vector3(330.8659f, 6.7168036f, -654.5339f), 99),
                    new PotChestData(new Vector3(-333.3444f, 2.9999998f, -861.1722f), 99),
                    new PotChestData(new Vector3(-313.2906f, 108.10962f, 70.76207f), 99),
                    new PotChestData(new Vector3(-459.1735f, 93.57443f, 5.054043f), 99),
                    new PotChestData(new Vector3(-54.69518f, 99.40573f, 405.0261f), 99),
                    new PotChestData(new Vector3(-382.4396f, 109.30187f, -378.3482f), 99),
                    new PotChestData(new Vector3(263.2559f, 100.38499f, 326.6834f), 99),
                    new PotChestData(new Vector3(224.7233f, 68.7328f, 518.668f), 99),
                    new PotChestData(new Vector3(19.73968f, 26.045855f, -420.977f), 99),
                    new PotChestData(new Vector3(705.2716f, 68.143616f, 358.6714f), 99),
                    new PotChestData(new Vector3(-660.5336f, 98f, -216.7666f), 99),
                    new PotChestData(new Vector3(-324.2736f, 121f, 203.2017f), 99),
                    new PotChestData(new Vector3(-386.5904f, -0.13994062f, -461.0976f), 99),
                ]
            },
            // South
            {
                1977, [
                    new PotChestData(new Vector3(-195.4419f, 110.15342f, -287.8911f), 99),
                    new PotChestData(new Vector3(74.73397f, 110.494316f, -394.1289f), 99),
                    new PotChestData(new Vector3(-386.437f, 98.60658f, -221.7847f), 99),
                    new PotChestData(new Vector3(-554.6146f, 99.01769f, -309.1231f), 99),
                    new PotChestData(new Vector3(107.0611f, 105.699875f, 146.7059f), 99),
                    new PotChestData(new Vector3(825.9521f, 70f, 772.4054f), 99),
                    new PotChestData(new Vector3(-836.7586f, 106.999985f, 597.2944f), 99),
                    new PotChestData(new Vector3(67.45271f, 69.477974f, 745.8658f), 99),
                    new PotChestData(new Vector3(69.70596f, 111.56108f, -239.064f), 99),
                    new PotChestData(new Vector3(301.8741f, 103.784424f, 70.59854f), 99),
                    new PotChestData(new Vector3(-38.97946f, 102.073296f, -175.4589f), 99),
                    new PotChestData(new Vector3(-60.72729f, 69.687035f, 828.4997f), 99),
                    new PotChestData(new Vector3(17.60418f, 65.93209f, 674.6207f), 99),
                    new PotChestData(new Vector3(393.2685f, 57.545956f, 844.6924f), 99),
                    new PotChestData(new Vector3(393.0191f, 104f, -124.1651f), 99),
                    new PotChestData(new Vector3(-798.7886f, 84.22545f, -4.822005f), 99),
                    new PotChestData(new Vector3(440.8355f, 70.3f, 876.4097f), 99),
                    new PotChestData(new Vector3(-734.1434f, 170.99998f, 683.7238f), 99),
                    new PotChestData(new Vector3(423.3505f, 70.3f, 578.9013f), 99),
                    new PotChestData(new Vector3(200.1241f, 56f, 624.2285f), 99),
                    new PotChestData(new Vector3(-603.3457f, 139f, 858.6771f), 99),
                    new PotChestData(new Vector3(-829.598f, 62.66814f, 66.82948f), 99),
                    new PotChestData(new Vector3(-645.3027f, 135.69208f, -73.54771f), 99),
                    new PotChestData(new Vector3(-836.1612f, 107f, 770.2822f), 99),
                    new PotChestData(new Vector3(-676.6202f, 128.57442f, 1.531581f), 99),
                    new PotChestData(new Vector3(-713.6796f, 203f, 710.08f), 99),
                    new PotChestData(new Vector3(781.2514f, 70f, 560.0701f), 99),
                    new PotChestData(new Vector3(-746.1318f, 172.00023f, 828.8809f), 99),
                    new PotChestData(new Vector3(-730.5441f, 107.694275f, -371.4776f), 99),
                    new PotChestData(new Vector3(-810.8279f, 114.053925f, -226.8324f), 99),
                ]
            },
        };
    }

    public override List<PotChestData> GetRerollPotChestData()
    {
        return
        [
            new PotChestData(new Vector3(-676.4631f, 5f, -769.7955f), 99),
            new PotChestData(new Vector3(-823.9183f, 140.00032f, 677.6934f), 99),
            new PotChestData(new Vector3(-886.4718f, 107f, 712.4964f), 99),
            new PotChestData(new Vector3(-625.7809f, 171f, 810.8691f), 99),
            new PotChestData(new Vector3(-813.9943f, 5f, -663.3634f), 99),
            new PotChestData(new Vector3(-842.8967f, 75.76903f, -125.0559f), 99),
            new PotChestData(new Vector3(-680.0345f, 201f, 739.9117f), 99),
            new PotChestData(new Vector3(-793.0552f, 5f, -777.3126f), 99),
            new PotChestData(new Vector3(-708.6777f, 171f, 669.5714f), 99),
            new PotChestData(new Vector3(-718.0424f, 5f, -633.8791f), 99),
            new PotChestData(new Vector3(-868.8489f, 67.5054f, -59.44909f), 99),
            new PotChestData(new Vector3(-803.5182f, 3f, -602.7497f), 99),
            new PotChestData(new Vector3(-732.2048f, 139f, 828.8491f), 99),
            new PotChestData(new Vector3(-659.1158f, 12.198493f, -508.7968f), 99),
            new PotChestData(new Vector3(-785.997f, 162.39513f, 790.5948f), 99),
            new PotChestData(new Vector3(-840.8771f, 107.26465f, -250.273f), 99),
            new PotChestData(new Vector3(-708.687f, 141.16982f, -139.3283f), 99),
            new PotChestData(new Vector3(-796.66f, 114.15647f, -228.9318f), 99),
            new PotChestData(new Vector3(-776.6315f, 5f, -486.978f), 99),
            new PotChestData(new Vector3(-758.8058f, 127.66496f, -183.164f), 99),
        ];
    }

    public override List<CarrotData> GetCarrotData()
    {
        return
        [
            new CarrotData(1, new Vector3(-554.02f, 110.70f, -365.90f), 11),
            new CarrotData(2, new Vector3(-710.27f, 3.00f, -451.51f), 23),
            new CarrotData(3, new Vector3(720.41f, 120.00f, 271.05f), 21),
            new CarrotData(4, new Vector3(-771.63f, 5.00f, -694.00f), 22),
            new CarrotData(5, new Vector3(845.53f, 98.00f, 777.43f), 17),
            new CarrotData(6, new Vector3(-701.88f, 201.00f, 718.72f), 28),
            new CarrotData(7, new Vector3(-843.86f, 83.66f, -36.78f), 24),
            new CarrotData(8, new Vector3(-84.74f, 3.00f, -796.02f), 1),
            new CarrotData(9, new Vector3(-490.32f, 3.00f, -741.02f), 22),
            new CarrotData(10, new Vector3(-727.85f, 81.48f, 328.93f), 19),
            new CarrotData(11, new Vector3(-273.09f, 75.00f, 850.03f), 20),
            new CarrotData(12, new Vector3(-806.51f, 107.00f, 887.61f), 26),
            new CarrotData(13, new Vector3(248.92f, 56.00f, 791.11f), 20),
            new CarrotData(14, new Vector3(827.20f, 108.00f, -156.44f), 5),
            new CarrotData(15, new Vector3(-743.60f, 96.39f, 84.44f), 13),
            new CarrotData(16, new Vector3(650.23f, 108.00f, 141.19f), 5),
            new CarrotData(17, new Vector3(-174.05f, 121.00f, 107.65f), 11),
            new CarrotData(18, new Vector3(-575.64f, 162.40f, 668.70f), 27),
            new CarrotData(19, new Vector3(-400.53f, 3.00f, -518.30f), 8),
            new CarrotData(20, new Vector3(865.00f, 96.00f, -214.67f), 5),
            new CarrotData(21, new Vector3(772.36f, 70.30f, 531.13f), 21),
            new CarrotData(22, new Vector3(466.20f, 70.30f, 563.25f), 17),
            new CarrotData(23, new Vector3(477.41f, 96.10f, 138.65f), 4),
            new CarrotData(24, new Vector3(283.65f, 56.00f, 587.31f), 15),
            new CarrotData(25, new Vector3(-439.05f, 115.82f, 184.47f), 12),
        ];
    }


    private readonly static AethernetData BaseCamp = new()
    {
        Id = 4944,
        BaseId = 2014664,
        Position = new Vector3(830.75f, 72.98f, -695.98f),
        Destination = new Vector3(835.3f, 73f, -695.9f),
        DeadRadius = 3f,
    };

    private readonly static AethernetData TheWanderersHaven = new()
    {
        Id = 4936,
        BaseId = 2014665,
        Position = new Vector3(-173.02f, 8.19f, -611.14f),
        Destination = new Vector3(-169.1f, 6.5f, -609.4f),
        DeadRadius = 3.2f,
    };

    private readonly static AethernetData CrystallizedCaverns = new()
    {
        Id = 4929,
        BaseId = 2014666,
        Position = new Vector3(-358.14f, 101.98f, -120.96f),
        Destination = new Vector3(-354.6f, 100f, -120.7f),
        DeadRadius = 3.2f,
    };

    private readonly static AethernetData Eldergrowth = new()
    {
        Id = 4930,
        BaseId = 2014667,
        Position = new Vector3(306.94f, 105.18f, 305.65f),
        Destination = new Vector3(-302.3f, 103f, 306f),
        DeadRadius = 3.2f,
    };

    private readonly static AethernetData Stonemarsh = new()
    {
        Id = 4942,
        BaseId = 2014744,
        Position = new Vector3(-384.12f, 99.20f, 281.42f),
        Destination = new Vector3(-384f, 97.2f, 278.1f),
        DeadRadius = 3.2f,
    };

    private IEnumerable<uint> KnowledgeCrystalLayoutIds
    {
        get =>
        [
            11617915, // Base Camp
            11617920, // Stonemarsh
            11617921, // The Wanderer's Haven
            11617922, // Eldergrowth
            11617923, // Crystalized Caverns
        ];
    }
}
