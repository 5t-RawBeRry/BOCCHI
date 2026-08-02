using System.Numerics;
namespace BOCCHI.Common.Data.Aethernet;

public class AethernetData
{
    // Graph routing / idle stand-off from the crystal or Destination pad.
    public const float InteractRadius = 4.3f;

    // Lifestream UI range from crystal center (OC bases are ~2y+; 1.9 sat inside the mesh).
    public const float LifestreamInteractRadius = 3.5f;

    public float DeadRadius { get; init; } = 3.2f;

    public uint Id { get; init; }

    public uint BaseId { get; init; }

    public Vector3 Position { get; init; }

    public Vector3 Destination { get; init; }
}
