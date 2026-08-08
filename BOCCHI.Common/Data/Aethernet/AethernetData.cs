using System.Numerics;
namespace BOCCHI.Common.Data.Aethernet;

public class AethernetData
{
    // Graph routing / idle stand-off from the crystal or Destination pad.
    public const float InteractRadius = 4.3f;

    /// <summary>
    ///     Hand off to Lifestream at this distance from crystal center (base camp and shards).
    ///     BOCCHI paths to <see cref="AethernetNavigation.CampApproachRadius"/> and stops — no closer.
    /// </summary>
    public const float LifestreamInteractRadius = 2.0f;

    public float DeadRadius { get; init; } = 3.2f;

    public uint Id { get; init; }

    public uint BaseId { get; init; }

    public Vector3 Position { get; init; }

    public Vector3 Destination { get; init; }
}
