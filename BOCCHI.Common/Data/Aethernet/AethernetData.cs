using System.Numerics;

namespace BOCCHI.Common.Data.Aethernet;

public class AethernetData
{
    /// <summary>Pad / interact proximity used by graph routing.</summary>
    public const float InteractRadius = 4.3f;

    /// <summary>Idle band width outside the magenta (Lifestream) ring.</summary>
    public const float LifestreamEdgeClearance = 2.0f;

    public const float DefaultDeadRadius = 3.2f;

    /// <summary>Magenta / Lifestream radius from <see cref="Position"/>.</summary>
    public float DeadRadius { get; init; } = DefaultDeadRadius;

    public uint Id { get; init; }

    public uint BaseId { get; init; }

    public Vector3 Position { get; init; }

    public Vector3 Destination { get; init; }
}
