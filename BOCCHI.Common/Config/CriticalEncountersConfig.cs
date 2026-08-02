using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 10, Order = 2)]
public class CriticalEncountersConfig : IAutoConfig
{
    [Checkbox]
    public bool ShouldDoCriticalEncounters { get; set; } = true;

    [DisabledCriticalEncounterIds]
    public HashSet<uint> DisabledCriticalEncounterIds { get; set; } = [];

    public bool IsCriticalEncounterEnabled(uint criticalEncounterId) =>
        ShouldDoCriticalEncounters && !DisabledCriticalEncounterIds.Contains(criticalEncounterId);
}
