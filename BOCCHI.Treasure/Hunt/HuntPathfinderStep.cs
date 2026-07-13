namespace BOCCHI.Treasure.Hunt;

public enum HuntPathfinderStepType
{
    WalkToNode,
    ReturnToBaseCamp,
    WalkToAethernet,
    TeleportToAethernet,
}

public enum HuntPathfinderState
{
    None,
    LoadingFile,
    FileLoaded,
    Pathfinding,
    PathfindingDone,
}

public enum HuntAethernet : uint
{
    BaseCamp = 4944,
    TheWanderersHaven = 4936,
    CrystallizedCaverns = 4929,
    Eldergrowth = 4930,
    Stonemarsh = 4942,
}

public class HuntPathfinderStep
{
    public HuntPathfinderStepType Type;

    public uint NodeId;

    public HuntAethernet Aethernet = HuntAethernet.BaseCamp;

    public static HuntPathfinderStep WalkToDestination(uint id)
    {
        return new HuntPathfinderStep
        {
            Type = HuntPathfinderStepType.WalkToNode,
            NodeId = id,
        };
    }

    public static HuntPathfinderStep WalkToAethernet(HuntAethernet aethernet)
    {
        return new HuntPathfinderStep
        {
            Type = HuntPathfinderStepType.WalkToAethernet,
            Aethernet = aethernet,
        };
    }

    public static HuntPathfinderStep TeleportToAethernet(HuntAethernet aethernet)
    {
        return new HuntPathfinderStep
        {
            Type = HuntPathfinderStepType.TeleportToAethernet,
            Aethernet = aethernet,
        };
    }

    public static HuntPathfinderStep ReturnToBaseCamp()
    {
        return new HuntPathfinderStep
        {
            Type = HuntPathfinderStepType.ReturnToBaseCamp,
        };
    }
}
