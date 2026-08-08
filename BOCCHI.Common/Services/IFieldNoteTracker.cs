using BOCCHI.Common.Data.EventDrops;

namespace BOCCHI.Common.Services;

public interface IFieldNoteTracker
{
    /// <summary>True when the Occult Record is unlocked or a matching Notes item is still in inventory.</summary>
    bool HasNote(MonsterNote note);

    bool NeedsNote(MonsterNote note) => !HasNote(note);

    /// <summary>True when the Occult Record checklist entry is complete.</summary>
    bool HasEntry(FieldNoteTargets.Entry entry);

    /// <summary>Completionist: pursue this FATE only if it drops a note we still need.</summary>
    bool ShouldPursueFate(uint fateId);

    /// <summary>Completionist: pursue this CE only if it drops a note we still need.</summary>
    bool ShouldPursueCriticalEncounter(uint encounterId);
}
