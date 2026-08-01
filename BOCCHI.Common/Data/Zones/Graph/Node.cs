using System.Numerics;
namespace BOCCHI.Common.Data.Zones.Graph;

public class Node
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public NodeType Type { get; set; }

    public Vector3 Position { get; set; }

    public INodeMetadata Metadata { get; set; } = new BlankNodeMetadata();

    public bool IsTeleport() => Type is NodeType.BaseCampAetheryte or NodeType.AethernetShard;
}
