namespace BOCCHI.Treasure.Hunt;

/// <summary>
/// Shared treasure / carrot hunt distances (empty-skip, divert, pad match, carrot use).
/// </summary>
public static class HuntDistances
{
    /// <summary>Match live objects to a layout/authored pad within this distance.</summary>
    public const float MatchRadius = 120f;

    public const float MatchRadiusSq = MatchRadius * MatchRadius;

    /// <summary>Same-pad double-spawn recheck after opening a bunny.</summary>
    public const float SamePadRecheckRadius = 20f;

    public const float SamePadRecheckRadiusSq = SamePadRecheckRadius * SamePadRecheckRadius;

    /// <summary>
    /// Player within this of a pad with no live object ⇒ empty
    /// (same object-table idea as Umbra).
    /// </summary>
    public const float EmptyPadSkipRadius = 150f;

    /// <summary>
    /// If any related object is streamed within this of the pad, the area is loaded
    /// and absence at the pad can be trusted without walking closer.
    /// </summary>
    public const float EmptyPadRegionTrustRadius = 200f;

    public const float EmptyPadRegionTrustRadiusSq = EmptyPadRegionTrustRadius * EmptyPadRegionTrustRadius;

    /// <summary>Require a short empty confirmation so object-table flicker does not false-skip.</summary>
    public static readonly TimeSpan EmptyPadConfirmDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Close enough (2D) to use a Fortune Carrot. Keep near Pandora/chest interact range —
    /// 5–12y was stopping short of the pad.
    /// </summary>
    public const float UseRadius = 2.0f;

    /// <summary>3D interact range for bunny chests (same as coffer open).</summary>
    public const float BunnyInteractRadius = UseRadius;

    /// <summary>Still try interact if slightly outside preferred open distance.</summary>
    public const float BunnyMaxInteractRadius = 2.75f;

    /// <summary>Dismount once this close to a carrot or bunny.</summary>
    public const float DismountRadius = 15f;

    /// <summary>If approach stalls within this range (2D), try using / opening from here.</summary>
    public const float StuckNearRadius = 3.5f;

    public static readonly TimeSpan StuckNearTimeout = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Mid-route divert / tour prefer: live unused target within this of the player.
    /// </summary>
    public const float NearbyLiveDivertRange = 100f;

    /// <summary>
    /// Only divert when the current destination is at least this far (yalms, 2D).
    /// </summary>
    public const float NearbyLiveDivertMinCurrentDistance = 80f;
}
