using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace BOCCHI.Common.Services;

public static class LogMessageHelper
{
    public static string GetLogMessagePattern(IDataManager data, uint id)
    {
        var pattern = data.GetExcelSheet<LogMessage>().GetRow(id).Text.ToString();
        return Regex.Replace(pattern, @"<num\((\w+)\)>", m => $"(?<{m.Groups[1].Value}>\\d+)");
    }
}
