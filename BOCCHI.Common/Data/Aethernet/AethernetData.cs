using System.Numerics;
namespace BOCCHI.Common.Data.Aethernet;

public class AethernetData
{
    // Graph routing / idle stand-off from the crystal or Destination pad.
    public const float InteractRadius = 4.3f;

    // Lifestream UI range from crystal center (slightly generous so near-pad counts as "at aetheryte").
    public const float LifestreamInteractRadius = 4.5f;

    public float DeadRadius { get; init; } = 3.2f;

    public uint Id { get; init; }

    public uint BaseId { get; init; }

    public Vector3 Position { get; init; }

    public Vector3 Destination { get; init; }
}
