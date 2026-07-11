using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation")]
public class FatesConfig : IAutoConfig
{
    [Checkbox]
    public bool ShouldDoFates { get; set; } = true;

    [Checkbox]
    public bool PreferPotFates { get; set; } = false;

    [Checkbox]
    public bool ShouldFarmPotChests { get; set; } = false;

    [Checkbox]
    public bool ShouldFarmRerollPotChests { get; set; } = true;

    [ConfigHidden]
    public HashSet<uint> DisabledFateIds { get; set; } =
    [
        1965, // The Winged Terror
        1976, // Persistent Pots
        1977, // Pleading Pots
    ];

    public bool IsFateEnabled(uint fateId)
    {
        return ShouldDoFates && !DisabledFateIds.Contains(fateId);
    }
}
