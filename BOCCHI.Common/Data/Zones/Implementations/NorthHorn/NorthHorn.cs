using BOCCHI.Common.Data.Aethernet;
using BOCCHI.Common.Data.Zones.Graph;
using BOCCHI.Common.Data.Zones.Graph.Factory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Ocelot.Services.Logger;
using Ocelot.Services.Pathfinding;
using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Implementations.NorthHorn;

public class NorthHorn
(
    IObjectTable objects,
    IDalamudPluginInterface plugin,
    IGraphFactory graphs,
    IPathfinder pathfinder,
    ILogger logger
) : BaseZone(objects, plugin, graphs, pathfinder, logger, ZoneId.NorthHorn)
{
    // PlaceName IDs match Lifestream's North Horn aethernet list (xivapi PlaceName sheet).
    private static readonly AethernetData BaseCamp = new()
    {
        Id = 5571,
        BaseId = 0,
        Position = Map(39.1f, 38.0f, 70f),
        Destination = Map(39.3f, 38.3f, 70f),
        DeadRadius = 3f
    };

    private static readonly AethernetData TheCrownOfKarnak = new()
    {
        Id = 5576,
        BaseId = 2015434,
        Position = new(451.684f, 70.927f, 528.839f),
        Destination = new(453.657f, 70f, 533.76f),
        DeadRadius = 5.38f
    };

    private static readonly AethernetData SinkingSanctuary = new()
    {
        Id = 5572,
        BaseId = 0,
        Position = Map(40.0f, 3.9f, 70f),
        Destination = Map(40.1f, 4.0f, 70f),
        DeadRadius = 3.2f
    };

    private static readonly AethernetData SuspendedMasonry = new()
    {
        Id = 5573,
        BaseId = 0,
        Position = Map(11.1f, 38.8f, 70f),
        Destination = Map(11.2f, 38.9f, 70f),
        DeadRadius = 3.2f
    };

    private static readonly AethernetData MolderingOutskirts = new()
    {
        Id = 5574,
        BaseId = 0,
        Position = Map(7.4f, 14.0f, 70f),
        Destination = Map(7.5f, 14.1f, 70f),
        DeadRadius = 3.2f
    };

    private static readonly AethernetData UnhallowedHamlet = new()
    {
        Id = 5575,
        BaseId = 0,
        Position = Map(21.3f, 19.7f, 70f),
        Destination = Map(21.4f, 19.8f, 70f),
        DeadRadius = 3.2f
    };

    protected override uint BasecampPlaceNameId
    {
        get => 5571;
    }

    public override AethernetData GetMainAetheryte() => BaseCamp;

    public override Vector3 GetAetherytePosition() => BaseCamp.Position;

    public override Vector3 GetStartingPosition() => Map(39.4f, 38.2f, 70f);

    public override List<AethernetData> GetAetherytes() =>
    [
        BaseCamp,
        TheCrownOfKarnak,
        SinkingSanctuary,
        SuspendedMasonry,
        MolderingOutskirts,
        UnhallowedHamlet
    ];

    public override List<AethernetData> GetAethernetShards() =>
    [
        TheCrownOfKarnak,
        SinkingSanctuary,
        SuspendedMasonry,
        MolderingOutskirts,
        UnhallowedHamlet
    ];

    protected override ushort GetForkedTowerEventId() => 64;

    public override List<ActivityData> GetNormalFateData() =>
    [
        new(2081, Map(12.5f, 5.4f)), // A Rotten Affair
        new(2078, Map(13.4f, 16.3f)), // Allure of the Occult
        new(2075, Map(31.7f, 20.8f)), // Eye to Eye
        new(2082, Map(4.2f, 31.0f)), // Gale-force Encounter
        new(2079, Map(18.0f, 11.7f)), // Inconstant Gardener
        new(2074, Map(35.8f, 25.7f)), // Raging Thrall
        new(2083, Map(8.2f, 20.1f)), // Scale Model
        new(2076, Map(23.3f, 30.8f)), // Shoreline Showdown
        new(2080, Map(19.6f, 38.7f)), // Territorial Dispute
        new(2084, Map(24.2f, 7.3f)), // Thunderregnum
        new(2077, Map(28.0f, 16.4f)) // Waved Away
    ];

    public override List<ActivityData> GetPotFateData() =>
    [
        new(2072, Map(26.2f, 11.6f)), // Daylight Pottery (North)
        new(2073, Map(11.0f, 25.8f)) // In a Pot of Bother (South)
    ];

    public override List<ActivityData> GetCriticalEncounterData() =>
    [
        new(56, Map(26.1f, 28.6f), 25f), // A Beast Unleashed
        new(63, Map(31.4f, 15.2f), 25f), // Accept No Imitators
        new(62, Map(19.8f, 31.1f), 25f), // Ahead of the Competition
        new(59, Map(38.2f, 9.8f), 25f), // Appalling Behavior
        new(53, Map(7.7f, 24.5f), 25f), // Cursed Resurgence
        new(57, Map(25.9f, 4.3f), 25f), // Dark Artistry
        new(50, Map(17.2f, 20.2f), 25f), // Doubled Trouble
        new(58, Map(13.5f, 35.8f), 25f), // Familiar Tactics
        new(52, Map(34.7f, 34.5f), 25f), // Forbidden Folios
        new(54, Map(36.7f, 21.4f), 25f), // Imbalanced Diet
        new(61, Map(18.4f, 4.3f), 25f), // Lost on the Wind
        new(49, Map(4.1f, 10.3f), 25f), // Many Mouths to Feed
        new(51, Map(12.0f, 8.0f), 25f), // Quarried Away
        new(60, Map(24.3f, 35.6f), 25f), // Tiny Terror
        new(55, Map(24.9f, 18.8f), 25f) // Web of Terror
    ];

    // LGB instance IDs are retained for reference; matching uses world positions + aggro level.
    // Layer 43366 farm spots only — skips underground junk layers from the dump.
    public override List<TreasureData> GetTreasureData() =>
    [
        new(12685250, 20, new(676.997f, 190.978f, 957.447f)), // BaseCamp_1
        new(12685381, 20, new(812.000f, 192.000f, 669.000f)), // BaseCamp_2
        new(12685449, 21, new(673.740f, 161.165f, 729.666f)), // BaseCamp_3
        new(12685504, 21, new(758.147f, 130.000f, 506.813f)), // BaseCamp_4
        new(12685673, 23, new(719.348f, 69.655f, 268.304f)), // BaseCamp_5
        new(12685783, 24, new(449.408f, 0.147f, 105.235f)), // BaseCamp_6
        new(12684950, 30, new(-22.669f, 42.087f, 628.995f)), // CrownofKarnak_1
        new(12685564, 23, new(246.227f, 66.542f, 676.666f)), // CrownofKarnak_2
        new(12686296, 30, new(77.070f, 21.200f, 536.270f)), // CrownofKarnak_3
        new(12686297, 30, new(-12.099f, 66.651f, 773.863f)), // CrownofKarnak_4
        new(12686373, 23, new(447.886f, 62.906f, 463.345f)), // CrownofKarnak_5
        new(12686430, 30, new(222.912f, 90.400f, 913.629f)), // CrownofKarnak_6
        new(12686310, 33, new(-612.214f, 66.990f, 578.548f)), // SuspendedMasonry_1
        new(12686302, 31, new(-278.056f, 47.784f, 567.973f)), // SuspendedMasonry_2
        new(12686304, 32, new(-256.947f, 100.667f, 812.197f)), // SuspendedMasonry_3
        new(12686307, 32, new(-504.091f, 85.753f, 758.321f)), // SuspendedMasonry_4
        new(12685021, 41, new(-645.440f, 160.099f, 967.944f)), // SuspendedMasonry_5
        new(12686354, 41, new(-699.837f, 160.000f, 926.379f)), // SuspendedMasonry_6
        new(12686353, 41, new(-592.000f, 160.101f, 767.669f)), // SuspendedMasonry_7
        new(12686355, 42, new(-857.793f, 159.850f, 772.237f)), // SuspendedMasonry_8
        new(12686356, 42, new(-800.397f, 157.800f, 633.387f)), // SuspendedMasonry_9
        new(12686312, 34, new(-775.894f, 70.719f, 377.153f)), // SuspendedMasonry_10
        new(12686326, 34, new(-923.142f, 113.265f, 197.948f)), // SuspendedMasonry_11
        new(12686317, 34, new(-631.779f, 78.255f, 240.000f)), // SuspendedMasonry_12
        new(12686303, 33, new(-436.442f, 0.203f, 166.219f)), // SuspendedMasonry_13
        new(12685986, 36, new(-265.761f, 30.171f, -439.519f)), // MolderingOutskirts_1
        new(12686338, 36, new(-254.141f, 1.821f, -266.312f)), // MolderingOutskirts_2
        new(12685970, 25, new(-26.000f, 0.232f, -437.688f)), // MolderingOutskirts_3
        new(12686008, 27, new(-232.419f, 53.237f, -719.972f)), // MolderingOutskirts_4
        new(12686343, 38, new(-439.551f, 43.044f, -558.449f)), // MolderingOutskirts_5
        new(12686344, 38, new(-525.781f, 46.857f, -783.468f)), // MolderingOutskirts_6
        new(12686360, 46, new(-416.774f, 45.937f, -945.431f)), // MolderingOutskirts_7
        new(12686359, 46, new(-736.024f, 21.035f, -881.486f)), // MolderingOutskirts_8
        new(12685062, 45, new(-815.808f, -21.835f, -699.370f)), // MolderingOutskirts_9
        new(12686358, 45, new(-928.626f, -11.228f, -744.956f)), // MolderingOutskirts_10
        new(12686357, 45, new(-857.599f, -12.235f, -609.817f)), // MolderingOutskirts_11
        new(12686340, 45, new(-697.271f, 34.898f, -565.022f)), // MolderingOutskirts_12
        new(12686336, 35, new(-878.967f, 13.135f, -314.202f)), // MolderingOutskirts_13
        new(12686339, 37, new(-707.376f, 41.586f, -396.989f)), // MolderingOutskirts_14
        new(12686337, 36, new(-581.489f, 40.914f, -257.411f)), // MolderingOutskirts_15
        new(12684954, 35, new(-633.696f, 82.718f, -146.005f)), // MolderingOutskirts_16
        new(12686335, 35, new(-590.208f, 87.979f, -7.000f)), // MolderingOutskirts_17
        new(12686295, 29, new(389.536f, 60.682f, -733.018f)), // SinkingSanctuary_1
        new(12686027, 27, new(147.869f, 61.000f, -868.752f)), // SinkingSanctuary_2
        new(12684947, 27, new(-2.306f, 66.691f, -814.905f)), // SinkingSanctuary_3
        new(12685955, 25, new(254.744f, 36.932f, -605.000f)), // SinkingSanctuary_4
        new(12686410, 26, new(279.093f, 143.000f, -356.148f)), // SinkingSanctuary_5
        new(12685926, 25, new(478.451f, 12.422f, -202.971f)), // SinkingSanctuary_6
        new(12685867, 24, new(649.544f, 46.245f, -157.774f)), // SinkingSanctuary_7
        new(12684944, 25, new(383.314f, 33.000f, -175.648f)), // SinkingSanctuary_8
        new(12686029, 28, new(658.809f, 66.126f, -364.676f)), // SinkingSanctuary_9
        new(12686242, 43, new(658.723f, 60.520f, -552.306f)), // SinkingSanctuary_10
        new(12686350, 43, new(639.049f, 60.625f, -698.726f)), // SinkingSanctuary_11
        new(12685003, 43, new(634.792f, 60.515f, -831.787f)), // SinkingSanctuary_12
        new(12686349, 43, new(633.132f, 60.642f, -910.227f)), // SinkingSanctuary_13
        new(12686352, 44, new(865.457f, 70.215f, -874.087f)), // SinkingSanctuary_14
        new(12686351, 44, new(815.444f, 60.554f, -657.314f)), // SinkingSanctuary_15
        new(12686166, 28, new(950.201f, 74.000f, -358.976f)), // SinkingSanctuary_16
        new(12686346, 39, new(43.782f, 2.454f, -108.192f)), // UnhallowedHamlet_1
        new(12686345, 39, new(85.598f, 3.303f, -281.140f)), // UnhallowedHamlet_2
        new(12686347, 39, new(-168.204f, 3.380f, -153.458f)), // UnhallowedHamlet_3
        new(12686348, 40, new(-162.042f, 3.590f, 98.450f)), // UnhallowedHamlet_4
        new(12686420, 40, new(-287.741f, -92.000f, 125.666f)), // UnhallowedHamlet_5
        new(12686361, 47, new(-144.726f, -129.796f, 304.938f)), // UnhallowedHamlet_6
        new(12686362, 47, new(41.233f, -140.771f, 168.502f)), // UnhallowedHamlet_7
        new(12686363, 48, new(161.000f, -151.760f, 16.000f)), // UnhallowedHamlet_8
        new(12685147, 48, new(223.653f, -161.864f, -30.644f)), // UnhallowedHamlet_9
        new(12686364, 48, new(313.919f, -139.530f, 180.071f)) // UnhallowedHamlet_10
    ];

    /// <summary>
    /// Convert in-game map coords (X:Y) to world XZ. SizeFactor 100 / Offset 0 for territory 1346.
    /// Heights are approximate until in-zone dumps refine them.
    /// </summary>
    private static Vector3 Map(float mapX, float mapY, float y = 70f) =>
        new((mapX - 21.5f) * 50f, y, (mapY - 21.5f) * 50f);
}
