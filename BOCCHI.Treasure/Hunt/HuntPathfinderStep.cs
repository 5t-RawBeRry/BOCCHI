namespace BOCCHI.Treasure.Hunt;

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
