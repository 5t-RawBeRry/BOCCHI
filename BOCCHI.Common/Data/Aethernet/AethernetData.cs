using System.Numerics;

namespace BOCCHI.Common.Data.Aethernet;

public class AethernetData
{
    // Graph routing / idle stand-off from the crystal or Destination pad.
    public const float InteractRadius = 4.3f;

    /// <summary>
    ///     Width of the idle band outside the Lifestream (magenta) ring — cyan is
    ///     <see cref="DeadRadius"/> + this. Teleport requires being inside magenta.
    /// </summary>
    public const float LifestreamEdgeClearance = 2.0f;

    /// <summary>
    ///     Magenta / Lifestream radius from <see cref="Position"/> (at the solid model's edge).
    ///     Must be inside this to teleport; idle waits between this and +<see cref="LifestreamEdgeClearance"/>.
    /// </summary>
    public float DeadRadius { get; init; } = 3.2f;

    public uint Id { get; init; }

    public uint BaseId { get; init; }

    public Vector3 Position { get; init; }

    public Vector3 Destination { get; init; }
}
