using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>Illegal Mode FATE allowlist and skip-by-progress (Pots &amp; Treasure uses progress skip only).</summary>
[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 2)]
public class FatesConfig : IAutoConfig
{
    /// <summary>South / North Horn Magic Pot FATE ids (Persistent / Pleading / Daylight / Pot of Bother).</summary>
    public static readonly uint[] PotFateIds = [1976, 1977, 2072, 2073];

    /// <summary>
    ///     Skip FATEs at or above this progress % (0 = disabled). Once you are in the FATE, it is finished.
    /// </summary>
    [IntRange(0, 100, Order = 0, Section = "skip")]
    public int MaxFateProgressPercent { get; set; } = 50;

    [DisabledFateIds(Order = 1, Section = "allowlist")]
    public HashSet<uint> DisabledFateIds { get; set; } =
    [
        // South Horn — dangerous / usually skipped by default
        1965 // The Winged Terror
    ];

    public bool IsFateEnabled(uint fateId) => !DisabledFateIds.Contains(fateId);

    /// <summary>True when a live FATE should not be started / pathing to — already registered FATEs stay.</summary>
    public bool ShouldSkipByProgress(byte progress) =>
        MaxFateProgressPercent > 0 && progress >= MaxFateProgressPercent;

    /// <summary>
    ///     Prefer pot FATEs force-includes Magic Pots for Illegal Mode (shown locked on in Allowed FATEs).
    ///     Farm pot chests does not.
    /// </summary>
    public bool IsFateEnabledForIllegalMode(uint fateId, bool isPotFate, bool preferPotFates, bool shouldFarmPotChests)
    {
        _ = shouldFarmPotChests;
        if (IsFateEnabled(fateId))
        {
            return true;
        }

        return isPotFate && preferPotFates;
    }

    /// <summary>
    ///     Pot fallback cutoffs / preposition apply when either farm pot chests or wait-near-pots is
    ///     on, and that pot is allowed. Both reasons need the same window: something has to stop a
    ///     FATE/CE starting right before the pot, or there is no time to get there.
    ///     Prefer pot FATEs alone does not turn on pot timing.
    /// </summary>
    public bool IsPotFallbackGatingEnabled(
        uint predictedNextPotFateId,
        bool shouldDoFates,
        bool preferPotFates,
        bool shouldFarmPotChests,
        bool shouldPrepositionToPots)
    {
        if (!shouldDoFates || (!shouldFarmPotChests && !shouldPrepositionToPots))
        {
            return false;
        }

        if (predictedNextPotFateId == 0)
        {
            return false;
        }

        return IsFateEnabledForIllegalMode(
            predictedNextPotFateId,
            isPotFate: true,
            preferPotFates,
            shouldFarmPotChests);
    }
}
