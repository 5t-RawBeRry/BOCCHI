namespace BOCCHI.Treasure.Hunt;

/// <summary>Shared carrot hunt distances (also used by the debug export panel).</summary>
public static class CarrotHuntDistances
{
    /// <summary>Match live chewed carrots to an authored pad within this distance.</summary>
    public const float MatchRadius = 100f;

    public const float MatchRadiusSq = MatchRadius * MatchRadius;

    /// <summary>After opening a bunny, look for another chewed carrot this close before leaving the pad.</summary>
    public const float SamePadRecheckRadius = 20f;

    public const float SamePadRecheckRadiusSq = SamePadRecheckRadius * SamePadRecheckRadius;

    /// <summary>Skip empty pads once this close with no live chewed carrot nearby.</summary>
    public const float TetherRadius = 55f;

    /// <summary>Close enough to use a Fortune Carrot when the pad is hard to path onto.</summary>
    public const float UseRadius = 5f;

    /// <summary>Dismount once this close to a carrot or bunny.</summary>
    public const float DismountRadius = 15f;

    /// <summary>If approach stalls within this range, try using / opening from here.</summary>
    public const float StuckNearRadius = 12f;

    public static readonly TimeSpan StuckNearTimeout = TimeSpan.FromSeconds(6);
}
