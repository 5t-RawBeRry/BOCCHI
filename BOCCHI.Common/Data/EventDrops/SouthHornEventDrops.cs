namespace BOCCHI.Common.Data.EventDrops;

/// <summary>
///     Authored South Horn FATE/CE reward icons (demiatma / notes / soul shards).
///     North Horn is intentionally omitted until drop tables exist.
/// </summary>
public static class SouthHornEventDrops
{
    private static readonly Dictionary<uint, EventDropInfo> Fates = new()
    {
        [1962] = new(Demiatma.Azurite, null, null),
        [1963] = new(Demiatma.Azurite, null, null),
        [1964] = new(Demiatma.Orpiment, null, null),
        [1965] = new(Demiatma.Realgar, null, null),
        [1966] = new(Demiatma.Malachite, null, null),
        [1967] = new(Demiatma.Realgar, null, null),
        [1968] = new(Demiatma.Verdigris, null, null),
        [1969] = new(Demiatma.Verdigris, null, null),
        [1970] = new(Demiatma.Azurite, null, null),
        [1971] = new(Demiatma.Orpiment, null, null),
        [1972] = new(Demiatma.CaputMortuum, null, null),
        [1976] = new(Demiatma.Orpiment, MonsterNote.PersistentPots, null),
        [1977] = new(Demiatma.Verdigris, MonsterNote.PersistentPots, null),
    };

    private static readonly Dictionary<uint, EventDropInfo> CriticalEncounters = new()
    {
        [33] = new(Demiatma.Azurite, null, null),
        [34] = new(Demiatma.Orpiment, MonsterNote.BlackChocobos, SoulShard.Ranger),
        [35] = new(Demiatma.Azurite, MonsterNote.CrescentBerserker, SoulShard.Berserker),
        [36] = new(Demiatma.Azurite, null, null),
        [37] = new(Demiatma.Verdigris, MonsterNote.CloisterDemon, null),
        [38] = new(Demiatma.Malachite, null, null),
        [39] = new(Demiatma.Malachite, MonsterNote.MythicIdol, null),
        [40] = new(Demiatma.CaputMortuum, null, null),
        [41] = new(Demiatma.Realgar, MonsterNote.NymianPotaladus, null),
        [42] = new(Demiatma.CaputMortuum, null, SoulShard.Oracle),
        [43] = new(Demiatma.Realgar, null, null),
        [44] = new(Demiatma.Orpiment, null, null),
        [45] = new(Demiatma.Realgar, MonsterNote.TradeTortoise, null),
        [46] = new(Demiatma.CaputMortuum, null, null),
        [47] = new(Demiatma.Malachite, null, null),
        // 48 Forked Tower — no drop icons
    };

    public static bool TryGetFate(uint fateId, out EventDropInfo drops) =>
        Fates.TryGetValue(fateId, out drops);

    public static bool TryGetCriticalEncounter(uint encounterId, out EventDropInfo drops) =>
        CriticalEncounters.TryGetValue(encounterId, out drops);
}
