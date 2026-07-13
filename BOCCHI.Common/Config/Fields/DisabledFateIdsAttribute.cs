using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config.Fields;

public sealed class DisabledFateIdsAttribute()
    : UIFieldAttribute(typeof(Renderers.DisabledFateIdsRenderer));
