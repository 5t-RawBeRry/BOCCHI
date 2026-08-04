using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 3)]
public class CriticalEncountersConfig : IAutoConfig
{
    [DisabledCriticalEncounterIds(Order = 0)]
    public HashSet<uint> DisabledCriticalEncounterIds { get; set; } = [];

    public bool IsCriticalEncounterEnabled(uint criticalEncounterId) =>
        !DisabledCriticalEncounterIds.Contains(criticalEncounterId);
}
