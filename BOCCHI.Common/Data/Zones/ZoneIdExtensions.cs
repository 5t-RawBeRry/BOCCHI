namespace BOCCHI.Common.Data.Zones;

public static class ZoneIdExtensions
{
    /// <summary>On-disk folder name under Treasure/Data for authored hunt files.</summary>
    public static string TreasureDataFolder(this ZoneId zoneId) => zoneId switch
    {
        ZoneId.SouthHorn => "SouthHorn",
        ZoneId.NorthHorn => "NorthHorn",
        var _ => throw new NotSupportedException($"No treasure data folder for zone {zoneId}")
    };
}
