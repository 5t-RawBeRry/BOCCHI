using BOCCHI.Treasure.Hunt;

namespace BOCCHI.Debug.Panels;

/// <summary>
///     Write model for the treasure hunt bake. Kept separate from the runtime
///     <see cref="HuntNodeDataSchema"/>, which deliberately has no <c>Path</c>: routing only reads
///     the id and distance, and the polylines were ~170k points (the bulk of a ~6.9 MB file) that
///     the runtime parsed on every load and never touched.
///     Paths are omitted unless explicitly requested, so a normal re-bake stays small.
/// </summary>
public sealed class HuntNodeBakeSchema
{
    public Dictionary<uint, List<BakeToNode>> NodeToNodeDistances { get; set; } = [];

    public Dictionary<HuntAethernet, List<BakeToNode>> AethernetToNodeDistances { get; set; } = [];

    public Dictionary<uint, List<BakeToAethernet>> NodeToAethernetDistances { get; set; } = [];
}

public sealed class BakeToNode(uint id, float distance, List<HuntPosition>? path)
{
    public uint Id { get; set; } = id;

    public float Distance { get; set; } = distance;

    /// <summary>Null (and omitted from JSON) unless the bake was asked to include full paths.</summary>
    public List<HuntPosition>? Path { get; set; } = path;
}

public sealed class BakeToAethernet(HuntAethernet aethernet, float distance, List<HuntPosition>? path)
{
    public HuntAethernet Aethernet { get; set; } = aethernet;

    public float Distance { get; set; } = distance;

    /// <inheritdoc cref="BakeToNode.Path"/>
    public List<HuntPosition>? Path { get; set; } = path;
}
