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
    BaseCamp = 4944,
    TheWanderersHaven = 4936,
    CrystallizedCaverns = 4929,
    Eldergrowth = 4930,
    Stonemarsh = 4942
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
