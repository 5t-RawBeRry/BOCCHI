using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("event_drops", GroupOrder = 25)]
public class EventDropConfig : IAutoConfig
{
    [Checkbox(Order = 0)]
    public bool ShowDemiatmaDrops { get; set; } = true;

    [Checkbox(Order = 1)]
    public bool ShowNoteDrops { get; set; } = true;

    [Checkbox(Order = 2)]
    public bool ShowSoulShardDrops { get; set; } = true;

    public bool AnyEnabled => ShowDemiatmaDrops || ShowNoteDrops || ShowSoulShardDrops;
}
