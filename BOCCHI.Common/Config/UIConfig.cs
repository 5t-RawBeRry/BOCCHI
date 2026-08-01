using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("ux", GroupOrder = 0)]
public class UIConfig : IAutoConfig
{
    [Checkbox] public bool ShowExperienceTracker { get; set; } = true;

    [Checkbox] public bool ShowExperienceTrackerGraph { get; set; } = false;

    [Checkbox] public bool ShowCurrencyTracker { get; set; } = true;

    [Checkbox] public bool ShowCurrencyTrackerGraph { get; set; } = false;

    [Checkbox] public bool ShowWorldSection { get; set; } = true;

    [Checkbox] public bool ShowAutomationSection { get; set; } = true;

    [Checkbox] public bool ShowTreasureSection { get; set; } = true;
}
