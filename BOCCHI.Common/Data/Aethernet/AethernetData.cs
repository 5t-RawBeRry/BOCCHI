using System.Numerics;
namespace BOCCHI.Common.Data.Aethernet;

public class AethernetData
{
    // General aetheryte proximity — used for graph routing and idle positioning.
    public const float InteractRadius = 4.3f;

    // Distance to crystal Position where Lifestream's aethernet UI is usable.
    // Big OC aetheryte bases extend ~2y+ from center; 1.9 was inside the mesh.
    public const float LifestreamInteractRadius = 3.5f;

    public float DeadRadius { get; init; } = 3.2f;

    public uint Id { get; init; }

    public uint BaseId { get; init; }

    public Vector3 Position { get; init; }

    public Vector3 Destination { get; init; }
}
