namespace BOCCHI.Treasure.Hunt;

/// <summary>Shared carrot hunt distances (also used by the debug export panel).</summary>
public static class CarrotHuntDistances
{
    /// <summary>Match live chewed carrots to an authored pad within this distance.</summary>
    public const float MatchRadius = 100f;

    public const float MatchRadiusSq = MatchRadius * MatchRadius;

    /// <summary>Skip empty pads once this close with no live chewed carrot nearby.</summary>
    public const float TetherRadius = 55f;
}
