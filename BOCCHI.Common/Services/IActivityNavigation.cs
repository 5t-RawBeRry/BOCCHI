using System.Numerics;

namespace BOCCHI.Common.Services;

public interface IActivityNavigation
{
    bool CanPathfind { get; }

    bool CanTeleport(Vector3 destination, out string? disabledReason);

    /// <summary>FATE/CE-style path: aethernet hop when possible, then approach the activity area.</summary>
    void PathTo(Vector3 destination, string name, string id);

    /// <summary>
    ///     Survey / POI path: pick the cheaper of direct walk, nearby-shard Lifestream, or Return +
    ///     aethernet, then vnav (with mount). Does not use Illegal Mode or CE wait rings.
    /// </summary>
    void PathToPoint(Vector3 destination, string name, string id);

    void TeleportToward(Vector3 destination, string name, string id);
}
