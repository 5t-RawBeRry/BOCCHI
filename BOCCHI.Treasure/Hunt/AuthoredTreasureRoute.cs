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

    /// <summary>Optional half tag (e.g. red / blue for South Horn).</summary>
    public string? Half { get; set; }

    public List<uint> Nodes { get; set; } = [];

    /// <summary>Hop before the next segment. Omit or type "auto" to use bake GetBestSteps.</summary>
    public AuthoredTreasureTransition? TransitionAfter { get; set; }
}

public sealed class AuthoredTreasureTransition
{
    /// <summary>return | teleport | auto | none</summary>
    public string Type { get; set; } = "auto";

    /// <summary>HuntAethernet enum name when Type is teleport.</summary>
    public string? To { get; set; }
}

/// <summary>Flattened authored pad with optional transition that applies after this pad toward the next.</summary>
public readonly record struct AuthoredRouteEntry(uint NodeId, string? Half, AuthoredTreasureTransition? TransitionAfter);
