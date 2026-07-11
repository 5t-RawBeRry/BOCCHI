using BOCCHI.Common.Data.Zones;

namespace BOCCHI.Treasure.Hunt;

public static class HuntDataPaths
{
    public static string GetDataFile(ZoneId zoneId, string filename)
    {
        var zoneFolder = zoneId switch
        {
            ZoneId.SouthHorn => "SouthHorn",
            _ => throw new NotSupportedException($"No hunt data for zone {zoneId}"),
        };

        var assemblyDir = Path.GetDirectoryName(typeof(HuntDataPaths).Assembly.Location)!;
        return Path.Combine(assemblyDir, "Data", zoneFolder, filename);
    }
}
