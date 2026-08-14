using BOCCHI.Treasure.Hunt;

namespace BOCCHI.Debug.Panels;

/// <summary>
///     Write model for the treasure hunt bake. Separate from runtime
///     <see cref="HuntNodeDataSchema"/>, which has no <c>Path</c>. Paths are omitted unless requested.
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
