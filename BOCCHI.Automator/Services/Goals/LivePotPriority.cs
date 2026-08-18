using BOCCHI.Automator.Data;
using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;

namespace BOCCHI.Automator.Services.Goals;

/// <summary>
///     Live Magic Pot that Illegal Mode would actually start: Do FATEs, allowlist / Prefer,
///     skip-by-progress, late-pot skip, completionist.
/// </summary>
internal static class LivePotPriority
{
    public static bool IsStartable(
        Fate fate,
        IZone zone,
        AutomatorConfig automatorConfig,
        FatesConfig fatesConfig,
        PotsConfig potsConfig,
        IAutomatorContext automatorContext,
        IFieldNoteTracker fieldNotes)
    {
        if (!zone.IsPotFate(fate.Id.Value))
        {
            return false;
        }

        if (automatorContext.IsPotsAndTreasure)
        {
            return true;
        }

        if (!automatorConfig.ShouldDoFates
            || !fatesConfig.IsFateEnabledForIllegalMode(
                fate.Id.Value,
                isPotFate: true,
                automatorConfig.PreferPotFates))
        {
            return false;
        }

        if (automatorContext.IsCompletionist && !fieldNotes.ShouldPursueFate(fate.Id.Value))
        {
            return false;
        }

        return !fatesConfig.ShouldSkipByProgress(fate.Progress)
               && !potsConfig.ShouldSkipLivePot(fate.TimeRemainingSeconds);
    }

    public static Fate? FindStartable(
        IFateRepository fateRepository,
        IZoneProvider zones,
        AutomatorConfig automatorConfig,
        FatesConfig fatesConfig,
        PotsConfig potsConfig,
        IAutomatorContext automatorContext,
        IFieldNoteTracker fieldNotes)
    {
        IZone zone = zones.GetZone();
        return fateRepository.Snapshot()
            .FirstOrDefault(fate => IsStartable(
                fate,
                zone,
                automatorConfig,
                fatesConfig,
                potsConfig,
                automatorContext,
                fieldNotes));
    }
}
