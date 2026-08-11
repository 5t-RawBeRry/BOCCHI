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
    // South Horn — PlaceNameIds must match Lifestream / zone aethernet data
    BaseCamp = 4927,
    TheWanderersHaven = 4928,
    CrystallizedCaverns = 4929,
    Eldergrowth = 4930,
    Stonemarsh = 4942,

    // North Horn
    NorthHornBaseCamp = 5571,
    SinkingSanctuary = 5572,
    SuspendedMasonry = 5573,
    MolderingOutskirts = 5574,
    UnhallowedHamlet = 5575,
    TheCrownOfKarnak = 5576
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
