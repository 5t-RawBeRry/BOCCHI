using System.Numerics;
using Dalamud.Plugin.Services;

namespace BOCCHI.Treasure.Services;

/// <summary>
///     Shared enter/exit Hide requirement with clear-debounce so pack threats do not
///     burn Hide cooldown between nearby high-Knowledge mobs.
/// </summary>
public sealed class NinjaHideRouteGate
{
    /// <summary>No threat inside exit range for this long before Hide is no longer required.</summary>
    public static readonly TimeSpan ClearDebounce = TimeSpan.FromSeconds(2.5);

    /// <summary>Do not toggle Hide off for remount while a threat is still this close.</summary>
    public const float RemountClearMinYalms = 35f;

    private DateTime? clearCandidateSinceUtc;

    /// <summary>
    ///     Updates whether the route still needs Hide. Caller passes prior required flag.
    /// </summary>
    public bool UpdateRequired(
        IObjectTable objects,
        Vector3 playerPosition,
        bool currentlyRequired,
        bool isMounted,
        int knowledgeHideOffset,
        float enterDistance,
        float exitDistance)
    {
        if (KnowledgeThreat.TryFindIsleblazer(
                objects,
                playerPosition,
                KnowledgeThreat.IsleblazerUnhideDistance,
                out _))
        {
            ResetClearCandidate();
            return false;
        }

        if (KnowledgeThreat.TryGetPlayerForayLevel(objects) is not int foray)
        {
            ResetClearCandidate();
            return false;
        }

        int hideAt = KnowledgeThreat.HideAtOrAbove(foray, knowledgeHideOffset);
        float enter = enterDistance;
        if (isMounted)
        {
            enter += KnowledgeThreat.MountedThreatEnterBonus;
        }

        float exit = Math.Max(exitDistance, enter);

        if (currentlyRequired)
        {
            if (KnowledgeThreat.TryFindThreat(objects, playerPosition, hideAt, exit, out _, out _))
            {
                ResetClearCandidate();
                return true;
            }

            DateTime now = DateTime.UtcNow;
            clearCandidateSinceUtc ??= now;
            if (now - clearCandidateSinceUtc.Value < ClearDebounce)
            {
                return true;
            }

            ResetClearCandidate();
            return false;
        }

        ResetClearCandidate();
        return KnowledgeThreat.TryFindThreat(objects, playerPosition, hideAt, enter, out _, out _);
    }

    /// <summary>
    ///     True when a hide-eligible threat is still close enough that ending Hide to remount
    ///     would risk walking into the next pack member on cooldown.
    /// </summary>
    public bool ShouldKeepStealthForThreats(
        IObjectTable objects,
        Vector3 playerPosition,
        int knowledgeHideOffset,
        float exitDistance)
    {
        if (KnowledgeThreat.TryFindIsleblazer(
                objects,
                playerPosition,
                KnowledgeThreat.IsleblazerUnhideDistance,
                out _))
        {
            return false;
        }

        if (KnowledgeThreat.TryGetPlayerForayLevel(objects) is not int foray)
        {
            return false;
        }

        int hideAt = KnowledgeThreat.HideAtOrAbove(foray, knowledgeHideOffset);
        float remountClear = Math.Max(exitDistance, RemountClearMinYalms);
        return KnowledgeThreat.TryFindThreat(objects, playerPosition, hideAt, remountClear, out _, out _);
    }

    public void Reset() => ResetClearCandidate();

    private void ResetClearCandidate() => clearCandidateSinceUtc = null;
}
