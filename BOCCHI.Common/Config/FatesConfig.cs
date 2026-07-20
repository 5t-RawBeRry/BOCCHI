using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;
namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 1)]
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

    [DisabledFateIds]
    public HashSet<uint> DisabledFateIds { get; set; } =
    [
        1965, // The Winged Terror
        1976, // Persistent Pots
        1977 // Pleading Pots
    ];

    public bool IsFateEnabled(uint fateId) => ShouldDoFates && !DisabledFateIds.Contains(fateId);
}
