namespace BOCCHI.Treasure.Hunt;

/// <summary>schemaVersion 2 treasure_route.json — ordered segments with Return/teleport breaks.</summary>
public sealed class AuthoredTreasureRoute
{
    public int SchemaVersion { get; set; }

    public string Zone { get; set; } = "";

    public List<AuthoredTreasureSegment> Segments { get; set; } = [];
}

public sealed class AuthoredTreasureSegment
{
    public string Id { get; set; } = "";

    public List<uint> Nodes { get; set; } = [];

    /// <summary>Hop toward the next segment. Omit or type "auto" to use the cheapest bake hop.</summary>
    public AuthoredTreasureTransition? TransitionAfter { get; set; }
}

public sealed class AuthoredTreasureTransition
{
    /// <summary>return | teleport | auto | walk | none</summary>
    public string Type { get; set; } = "auto";

    /// <summary>HuntAethernet enum name when Type is teleport.</summary>
    public string? To { get; set; }
}

/// <summary>
///     Flattened authored pad. <paramref name="SegmentIndex"/> indexes into the planner's segment
///     list; -1 for pads that are live but absent from treasure_route.json.
/// </summary>
public readonly record struct AuthoredRouteEntry(uint NodeId, int SegmentIndex);

/// <summary>Segment metadata; TransitionAfter is on the segment, not the last pad.</summary>
public readonly record struct AuthoredRouteSegment(string Id, AuthoredTreasureTransition? TransitionAfter);
