namespace BOCCHI.Treasure.Hunt;

public struct HuntPosition
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }
}

/// <summary>
///     Runtime read model. The bake also emits a <c>path</c> polyline per pair, but routing only ever
///     needs the id and the distance — System.Text.Json skips the unread property, so those ~170k
///     points are never materialised. Keep it that way: adding <c>Path</c> back here costs a large
///     allocation on every load for data nothing consumes.
/// </summary>
public struct HuntToNode(uint id, float distance)
{
    public uint Id { get; set; } = id;

    public float Distance { get; set; } = distance;
}

/// <inheritdoc cref="HuntToNode"/>
public struct HuntToAethernet(HuntAethernet aethernet, float distance)
{
    public HuntAethernet Aethernet { get; set; } = aethernet;

    public float Distance { get; set; } = distance;
}

public class HuntNodeDataSchema
{
    public Dictionary<uint, List<HuntToNode>> NodeToNodeDistances { get; set; } = [];

    public Dictionary<HuntAethernet, List<HuntToNode>> AethernetToNodeDistances { get; set; } = [];

    public Dictionary<uint, List<HuntToAethernet>> NodeToAethernetDistances { get; set; } = [];
}
