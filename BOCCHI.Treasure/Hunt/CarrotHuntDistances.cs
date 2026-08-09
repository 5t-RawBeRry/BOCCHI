namespace BOCCHI.Treasure.Hunt;

/// <summary>Shared carrot hunt distances (also used by the debug export panel).</summary>
public static class CarrotHuntDistances
{
    /// <summary>Match live chewed carrots / estimate levels within this distance of a pad.</summary>
    public const float MatchRadius = 80f;

    public const float MatchRadiusSq = MatchRadius * MatchRadius;

    /// <summary>Arrive / skip empty pads once within this range (same idea as coffer search).</summary>
    public const float TetherRadius = 25f;
}
