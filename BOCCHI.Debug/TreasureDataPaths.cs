namespace BOCCHI.Debug;

/// <summary>Shared path helpers for debug export panels that write into BOCCHI.Treasure/Data.</summary>
public static class TreasureDataPaths
{
    public static string? FindRepoTreasureDataRoot(string? start)
    {
        string? dir = start;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "BOCCHI.Treasure", "Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }
}
