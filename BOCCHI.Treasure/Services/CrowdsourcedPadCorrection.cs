using System.Numerics;

namespace BOCCHI.Treasure.Services;

/// <summary>
///     When Share maps is on, accepted crowd pads can replace a wrong bake while keeping
///     baked ids (path overrides / levels). Close matches stay on bake; far same-id (coffers)
///     or mutual-nearest (carrots) snaps to the crowd centroid.
/// </summary>
public static class CrowdsourcedPadCorrection
{
    /// <summary>
    ///     Carrots have no stable shared id — only overwrite when bake and remote pick each
    ///     other as nearest and disagree beyond <see cref="CarrotPadCatalog.MergeRadius"/>.
    /// </summary>
    public const float CarrotMaxCorrection = 50f;

    public const float CarrotMaxCorrectionSq = CarrotMaxCorrection * CarrotMaxCorrection;

    /// <summary>
    ///     Same coffer <paramref name="dataId"/> in the accepted catalog, far from
    ///     <paramref name="current"/> → crowd centroid; otherwise keep <paramref name="current"/>.
    /// </summary>
    public static Vector3 CorrectCofferPosition(
        uint dataId,
        Vector3 current,
        IReadOnlyList<CrowdsourcedCofferCandidate> crowd)
    {
        Vector3? best = null;
        float bestDistSq = float.MaxValue;
        foreach (CrowdsourcedCofferCandidate candidate in crowd)
        {
            if (candidate.DataId != dataId || TreasurePathing.IsUnloadAltitude(candidate.Position))
            {
                continue;
            }

            float distSq = Vector3.DistanceSquared(candidate.Position, current);
            if (distSq >= bestDistSq)
            {
                continue;
            }

            bestDistSq = distSq;
            best = candidate.Position;
        }

        if (best is { } corrected && bestDistSq > CofferLocationSyncService.MatchRadiusSq)
        {
            return corrected;
        }

        return current;
    }

    /// <summary>
    ///     True when <paramref name="corrected"/> should replace the bake (caller logs).
    /// </summary>
    public static bool IsCofferCorrection(Vector3 before, Vector3 after) =>
        Vector3.DistanceSquared(before, after) > CofferLocationSyncService.MatchRadiusSq;
}
