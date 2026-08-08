using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

/// <summary>Illegal Mode FATE allowlist (not used by Pots &amp; Treasure for pot selection).</summary>
[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 2)]
public class FatesConfig : IAutoConfig
{
    [DisabledFateIds(Order = 0)]
    public HashSet<uint> DisabledFateIds { get; set; } =
    [
        // South Horn
        1965, // The Winged Terror
        1976, // Persistent Pots
        1977, // Pleading Pots
        // North Horn
        2072, // Daylight Pottery
        2073 // In a Pot of Bother
    ];

    public bool IsFateEnabled(uint fateId) => !DisabledFateIds.Contains(fateId);

    /// <summary>
    ///     Pot fallback cutoffs only apply when pot farming is on AND the predicted next pot FATE is enabled.
    ///     Disabled pot FATEs must not idle the automator near spawn.
    /// </summary>
    public bool IsPotFallbackGatingEnabled(
        uint predictedNextPotFateId,
        bool shouldDoFates,
        bool preferPotFates,
        bool shouldFarmPotChests)
    {
        if (!shouldDoFates || (!shouldFarmPotChests && !preferPotFates))
        {
            return false;
        }

        if (predictedNextPotFateId == 0)
        {
            return false;
        }

        return IsFateEnabled(predictedNextPotFateId);
    }
}
