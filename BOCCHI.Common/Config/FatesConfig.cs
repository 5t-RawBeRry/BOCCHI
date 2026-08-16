using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>Illegal Mode FATE allowlist (not used by Pots &amp; Treasure for pot selection).</summary>
[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 2)]
public class FatesConfig : IAutoConfig
{
    /// <summary>South / North Horn Magic Pot FATE ids (Persistent / Pleading / Daylight / Pot of Bother).</summary>
    public static readonly uint[] PotFateIds = [1976, 1977, 2072, 2073];

    [DisabledFateIds(Order = 0, Section = "allowlist")]
    public HashSet<uint> DisabledFateIds { get; set; } =
    [
        // South Horn — dangerous / usually skipped by default
        1965 // The Winged Terror
    ];

    public bool IsFateEnabled(uint fateId) => !DisabledFateIds.Contains(fateId);

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
    ///     Pot fallback cutoffs / preposition only apply when farm pot chests is on and that pot is allowed.
    ///     Prefer pot FATEs alone does not turn on wait-near-pots or pot timing.
    /// </summary>
    public bool IsPotFallbackGatingEnabled(
        uint predictedNextPotFateId,
        bool shouldDoFates,
        bool preferPotFates,
        bool shouldFarmPotChests)
    {
        if (!shouldDoFates || !shouldFarmPotChests)
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
