using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph;

public enum NodeType
{
    // Teleports
    BaseCampReturnPosition,
    BaseCampAetheryte,
    AethernetShard,

    // Activities
    NormalFate,
    PotFate,
    CriticalEncounter,

    // Points of interest
    KnowledgeCrystal,
    Treasure,
    _SilverChest, // unused (left for type id)
    PotChest,
    _PotChestB, // unused (left for type id)
    PostChestReroll, // Getting a reroll on any pot chest from any pool (including this one) will roll in this pool
    Carrot
}

public enum EdgeType
{
    Teleport,
    Walk
}

public class Node
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public NodeType Type { get; set; }

    public Vector3 Position { get; set; }

    public INodeMetadata Metadata { get; set; } = new BlankNodeMetadata();

    public bool IsTeleport() => Type is NodeType.BaseCampAetheryte or NodeType.AethernetShard;
}

public class Edge
{
    public Guid From { get; set; }

    public EdgeType Type { get; set; }

    public Guid To { get; set; }

    public float Cost { get; set; }
}
