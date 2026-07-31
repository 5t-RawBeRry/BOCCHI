namespace BOCCHI.Treasure.Hunt;

public enum HuntPathfinderStepType
{
    WalkToNode,
    ReturnToBaseCamp,
    WalkToAethernet,
    TeleportToAethernet
}

public enum HuntPathfinderState
{
    None,
    LoadingFile,
    FileLoaded,
    Pathfinding,
    PathfindingDone
}

public enum HuntAethernet : uint
{
    // South Horn
    BaseCamp = 4944,
    TheWanderersHaven = 4936,
    CrystallizedCaverns = 4929,
    Eldergrowth = 4930,
    Stonemarsh = 4942,

    // North Horn (PlaceName IDs — same values Lifestream uses)
    NorthHornBaseCamp = 5571,
    SinkingSanctuary = 5572,
    SuspendedMasonry = 5573,
    MolderingOutskirts = 5574,
    UnhallowedHamlet = 5575,
    TheCrownOfKarnak = 5576
}

public static class HuntAethernetExtensions
{
    public static bool IsBaseCamp(this HuntAethernet aethernet) =>
        aethernet is HuntAethernet.BaseCamp or HuntAethernet.NorthHornBaseCamp;
}

public class HuntPathfinderStep
{
    public HuntAethernet Aethernet = HuntAethernet.BaseCamp;

    public uint NodeId;
    public HuntPathfinderStepType Type;

    public static HuntPathfinderStep WalkToDestination(uint id) =>
        new()
        {
            Type = HuntPathfinderStepType.WalkToNode,
            NodeId = id
        };

    public static HuntPathfinderStep WalkToAethernet(HuntAethernet aethernet) =>
        new()
        {
            Type = HuntPathfinderStepType.WalkToAethernet,
            Aethernet = aethernet
        };

    public static HuntPathfinderStep TeleportToAethernet(HuntAethernet aethernet) =>
        new()
        {
            Type = HuntPathfinderStepType.TeleportToAethernet,
            Aethernet = aethernet
        };

    public static HuntPathfinderStep ReturnToBaseCamp() =>
        new()
        {
            Type = HuntPathfinderStepType.ReturnToBaseCamp
        };
}
