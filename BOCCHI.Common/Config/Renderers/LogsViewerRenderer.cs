using System.Reflection;
using BOCCHI.Common.Config.Fields;
using BOCCHI.Common.Services.Logging;
using Ocelot.Config.Renderers;
using Ocelot.Services.Translation;

namespace BOCCHI.Common.Config.Renderers;

public sealed class LogsViewerRenderer(BocchiLogsPanel panel) : IFieldRenderer<LogsViewerAttribute>
{
    public bool Render(object target, PropertyInfo prop, LogsViewerAttribute attr, Type owner, ITranslator translator)
    {
        panel.Draw(translator.WithScope("windows.logs"), idSuffix: "_config");
        return false;
    }
}
