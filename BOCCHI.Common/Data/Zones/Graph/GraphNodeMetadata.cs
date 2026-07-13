using System.Numerics;

namespace BOCCHI.Common.Data.Zones.Graph;

public interface INodeMetadata;

public class BlankNodeMetadata : INodeMetadata;

public class CarrotNodeMetaData : INodeMetadata
{
    public int Level { get; set; }
}

public class PotChestNodeMetaData : INodeMetadata
{
    public int FateId { get; set; }

    public int Level { get; set; }
}

public class RerollPotChestNodeMetaData : INodeMetadata
{
    public int Level { get; set; }
}

public class ActivityNodeMetadata : INodeMetadata
{
    public bool Available { get; set; } = false;

    public int Id { get; set; }
}

public class TeleportNodeMetadata : INodeMetadata
{
    public uint AetheryteId { get; set; } = 0;

    public float InteractRange { get; set; } = 3f;

    public Vector3 Destination { get; set; } = Vector3.Zero;

    public bool Unlocked { get; set; } = false;
}

public enum TreasureType
{
    Silver,
    Bronze,
}

public class TreasureNodeMetaData : INodeMetadata
{
    public TreasureType Type { get; set; }

    public int Level { get; set; }
}
