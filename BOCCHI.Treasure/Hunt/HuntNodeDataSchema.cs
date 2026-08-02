namespace BOCCHI.Treasure.Hunt;

public struct HuntPosition
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }
}

public struct HuntToNode(uint id, float distance, List<HuntPosition> path)
{
    public uint Id { get; set; } = id;

    public float Distance { get; set; } = distance;

    public List<HuntPosition> Path { get; set; } = path;
}

public struct HuntToAethernet(HuntAethernet aethernet, float distance, List<HuntPosition> path)
{
    public HuntAethernet Aethernet { get; set; } = aethernet;

    public float Distance { get; set; } = distance;

    public List<HuntPosition> Path { get; set; } = path;
}

public class HuntNodeDataSchema
{
    public Dictionary<uint, List<HuntToNode>> NodeToNodeDistances { get; set; } = [];

    public Dictionary<HuntAethernet, List<HuntToNode>> AethernetToNodeDistances { get; set; } = [];

    public Dictionary<uint, List<HuntToAethernet>> NodeToAethernetDistances { get; set; } = [];
}
