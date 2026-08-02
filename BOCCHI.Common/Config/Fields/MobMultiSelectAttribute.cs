using BOCCHI.Common.Config.Renderers;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config.Fields;

public sealed class MobMultiSelectAttribute()
    : UIFieldAttribute(typeof(MobMultiSelectRenderer));
