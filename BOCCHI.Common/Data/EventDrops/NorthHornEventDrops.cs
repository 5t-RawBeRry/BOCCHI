namespace BOCCHI.Common.Data.EventDrops;

/// <summary>
///     Authored North Horn FATE/CE reward icons (notes / soul shards where known).
/// </summary>
public static class NorthHornEventDrops
{
    private static readonly Dictionary<uint, EventDropInfo> Fates = new()
    {
        [2072] = new(null, MonsterNote.PersistentPots, null), // Daylight Pottery
        [2073] = new(null, MonsterNote.PersistentPots, null), // In a Pot of Bother
    };

    private static readonly Dictionary<uint, EventDropInfo> CriticalEncounters = new()
    {
        [50] = new(null, MonsterNote.ConjuredCalofisteri, null), // Doubled Trouble
        [51] = new(null, MonsterNote.AlabasterBlade, null), // Quarried Away
        [52] = new(null, MonsterNote.Arbatel, null), // Forbidden Folios
        [53] = new(null, MonsterNote.ClaretDragon, null), // Cursed Resurgence
        [54] = new(null, MonsterNote.Algol, null), // Imbalanced Diet
        [57] = new(null, MonsterNote.PhantomNecromancer, null), // Dark Artistry
        [59] = new(null, MonsterNote.Pallmagia, null), // Appalling Behavior
        [60] = new(null, MonsterNote.TinyMage, null), // Tiny Terror
        [61] = new(null, MonsterNote.Abductor, null), // Lost on the Wind
        [63] = new(null, MonsterNote.Metamorph, null), // Accept No Imitators
    };

    public static bool TryGetFate(uint fateId, out EventDropInfo drops) =>
        Fates.TryGetValue(fateId, out drops);

    public static bool TryGetCriticalEncounter(uint encounterId, out EventDropInfo drops) =>
        CriticalEncounters.TryGetValue(encounterId, out drops);
}
