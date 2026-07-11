using System.Globalization;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace BOCCHI.Common.Data.Mobs;

public static class MobData
{
    private static readonly Dictionary<Mob, string> NameCache = [];

    private static readonly Dictionary<Mob, Mob> LegacyToCrescent = new()
    {
        { Mob.Goobbue, Mob.Goobbue2 },
        { Mob.Taurus, Mob.Taurus2 },
        { Mob.Headstone, Mob.Headstone2 },
        { Mob.Garula, Mob.Garula2 },
        { Mob.VoidViper, Mob.VoidViper2 },
    };

    private static readonly HashSet<Mob> HiddenLegacyMobs = LegacyToCrescent.Keys.ToHashSet();

    public static IReadOnlyList<Mob> MobsWithSpawnCondition { get; } =
    [
        Mob.Armor,
        Mob.Bomb,
        Mob.Caoineag,
        Mob.Dhruva,
        Mob.Dullahan,
        Mob.Fool,
        Mob.Geshunpest,
        Mob.Ghost,
        Mob.Gourmand,
        Mob.Mimic,
        Mob.Mousse,
        Mob.Troubadour,
    ];

    public static IEnumerable<Mob> GetSelectableMobs()
    {
        return Enum.GetValues<Mob>().Where(m => !HiddenLegacyMobs.Contains(m));
    }

    public static bool TryFromNameId(uint nameId, out Mob mob)
    {
        mob = (Mob)nameId;
        if (Enum.IsDefined(mob))
        {
            return true;
        }

        mob = default;
        return false;
    }

    public static bool IsSelected(uint nameId, IReadOnlyCollection<Mob> selected)
    {
        if (!TryFromNameId(nameId, out var mob))
        {
            return false;
        }

        if (selected.Contains(mob))
        {
            return true;
        }

        if (LegacyToCrescent.TryGetValue(mob, out var crescent) && selected.Contains(crescent))
        {
            return true;
        }

        foreach (var (legacy, crescentMob) in LegacyToCrescent)
        {
            if (mob == crescentMob && selected.Contains(legacy))
            {
                return true;
            }
        }

        return false;
    }

    public static string GetDisplayName(Mob mob, IDataManager data)
    {
        if (NameCache.TryGetValue(mob, out var cached))
        {
            return FormatDisplayName(mob, cached);
        }

        if (data.GetExcelSheet<BNpcName>().TryGetRow((uint)mob, out var row))
        {
            var titleCase = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(row.Singular.ToString().ToLower());
            NameCache[mob] = titleCase;
            return FormatDisplayName(mob, titleCase);
        }

        return mob.ToString();
    }

    public static bool MatchesSearch(Mob mob, string search, IDataManager data)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;
        var displayName = GetDisplayName(mob, data);

        return displayName.Contains(search, comparison)
               || mob.ToString().Contains(search, comparison)
               || ((uint)mob).ToString().Contains(search, comparison);
    }

    private static string FormatDisplayName(Mob mob, string baseName)
    {
        if (LegacyToCrescent.ContainsValue(mob))
        {
            return $"{baseName} (Crescent)";
        }

        return baseName;
    }
}
