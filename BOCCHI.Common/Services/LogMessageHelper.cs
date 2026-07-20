using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Text.RegularExpressions;
namespace BOCCHI.Common.Services;

public static class LogMessageHelper
{
    public static string GetLogMessagePattern(IDataManager data, uint id)
    {
        string pattern = data.GetExcelSheet<LogMessage>().GetRow(id).Text.ToString();
        return Regex.Replace(pattern, @"<num\((\w+)\)>", m => $"(?<{m.Groups[1].Value}>\\d+)");
    }
}
