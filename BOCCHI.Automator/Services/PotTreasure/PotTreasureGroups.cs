using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Zones.Graph;
using System.Numerics;

namespace BOCCHI.Automator.Services.PotTreasure;

/// <summary>
///     Directional pot-chest candidates for Magical Elixir hints.
///     South Horn uses authored directional groups; North Horn (and any other zone) bins authored
///     flat <see cref="IZone.GetPotChestData"/> positions by compass bearing from the pot center.
/// </summary>
public static class PotTreasureGroups
{
    private static readonly string[] Octants =
    [
        "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest",
    ];

    public static bool CanRunSmart(IZone zone, int fateId) =>
        zone.IsPotFate(fateId)
        && (SouthHornPotTreasureGroups.HasGroups(fateId)
            || zone.GetPotChestData().ContainsKey(fateId));

    public static bool TryGetGroup(
        int fateId,
        string groupKey,
        Vector3 fateCenter,
        IZone zone,
        out IReadOnlyList<PotTreasureCandidate> candidates)
    {
        if (SouthHornPotTreasureGroups.TryGetGroup(fateId, groupKey, out candidates))
        {
            return true;
        }

        candidates = Array.Empty<PotTreasureCandidate>();
        if (!zone.GetPotChestData().TryGetValue(fateId, out List<PotChestData>? flat)
            || flat.Count == 0
            || string.IsNullOrEmpty(groupKey))
        {
            return false;
        }

        List<PotTreasureCandidate> binned = [];
        int index = 0;
        foreach (PotChestData chest in flat)
        {
            if (!TryDirectionKey(fateCenter, chest.Position, out string key) || key != groupKey)
            {
                continue;
            }

            index++;
            string prefix = groupKey switch
            {
                "north" => "N",
                "northeast" => "NE",
                "east" => "E",
                "southeast" => "SE",
                "south" => "S",
                "southwest" => "SW",
                "west" => "W",
                "northwest" => "NW",
                _ => "C",
            };
            binned.Add(new PotTreasureCandidate($"{prefix}{index}", chest.Position, chest.Level));
        }

        candidates = binned;
        return binned.Count > 0;
    }

    /// <summary>
    ///     When a compass bin is empty (common on North Horn flat lists), pick the nearest
    ///     non-empty adjacent octant so elixir redirects do not stall.
    /// </summary>
    public static bool TryGetNearestNonEmptyGroup(
        int fateId,
        string preferredKey,
        Vector3 fateCenter,
        IZone zone,
        out string groupKey,
        out IReadOnlyList<PotTreasureCandidate> candidates)
    {
        groupKey = string.Empty;
        candidates = Array.Empty<PotTreasureCandidate>();

        int start = Array.IndexOf(Octants, preferredKey);
        if (start < 0)
        {
            return false;
        }

        for (int step = 1; step <= 4; step++)
        {
            int[] deltas = [step, -step];
            foreach (int delta in deltas)
            {
                string key = Octants[(start + delta + 8) % 8];
                if (TryGetGroup(fateId, key, fateCenter, zone, out IReadOnlyList<PotTreasureCandidate> group)
                    && group.Count > 0)
                {
                    groupKey = key;
                    candidates = group;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Compass octant from pot center toward a chest (FFXIV: −Z is north).</summary>
    public static bool TryDirectionKey(Vector3 from, Vector3 to, out string groupKey)
    {
        groupKey = string.Empty;
        float dx = to.X - from.X;
        float dz = to.Z - from.Z;
        if (dx * dx + dz * dz < 1f)
        {
            return false;
        }

        // 0° = north (−Z), 90° = east (+X).
        float deg = MathF.Atan2(dx, -dz) * (180f / MathF.PI);
        if (deg < 0f)
        {
            deg += 360f;
        }

        int octant = (int)MathF.Floor((deg + 22.5f) / 45f) % 8;
        if (octant < 0 || octant >= Octants.Length)
        {
            return false;
        }

        groupKey = Octants[octant];
        return true;
    }
}
