using System.Numerics;

namespace BOCCHI.Common.Services;

public interface IActivityNavigation
{
    bool CanPathfind { get; }

    bool CanTeleport(Vector3 destination, out string? disabledReason);

    /// <summary>FATE/CE-style path: aethernet hop when possible, then approach the activity area.</summary>
    void PathTo(Vector3 destination, string name, string id);

    /// <summary>
    ///     Survey / POI path: aethernet (Lifestream) when at a shard, then vnav to the point with mount.
    ///     Does not use Illegal Mode routing or CE wait rings.
    /// </summary>
    void PathToPoint(Vector3 destination, string name, string id);

    /// <summary>
    ///     After a map flag is set: Lifestream to the best aethernet shard, then vnav to the flag
    ///     via <c>FlagToPoint</c> (same targeting as <c>/vnav moveflag</c>).
    /// </summary>
    void PathToFlag(string name, string id);

    void TeleportToward(Vector3 destination, string name, string id);
}
