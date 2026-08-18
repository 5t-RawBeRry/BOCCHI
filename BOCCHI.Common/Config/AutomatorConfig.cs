using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 0)]
public class AutomatorConfig : IAutoConfig
{
    [Checkbox(Order = 0, Section = "activities")]
    public bool ShouldDoFates { get; set; } = true;

    [Checkbox(Order = 1, Section = "activities")]
    public bool PreferPotFates { get; set; } = false;

    [Checkbox(Order = 2, Section = "activities")]
    public bool ShouldFarmPotChests { get; set; } = false;

    [Checkbox(Order = 3, Section = "activities")]
    public bool ShouldPrepositionToPots { get; set; } = true;

    [Checkbox(Order = 4, Section = "activities")]
    public bool ShouldDoCriticalEncounters { get; set; } = true;

    /// <summary>
    ///     While still walking to a FATE, only switch to a CE when registration has this many
    ///     seconds or fewer left. 0 = switch as soon as a CE is up (old behaviour). Once you are
    ///     in the FATE, it still finishes first (#187).
    /// </summary>
    [IntRange(0, 180, Order = 5, Section = "activities")]
    public int LeaveFateTravelForCeSeconds { get; set; } = 90;

    /// <summary>
    ///     Illegal Mode combat automation: Wrath/RSR + BOCCHI AI, or full BossMod / BMR autorotation.
    /// </summary>
    [EnumSelect<CombatAutorotation, CombatAutorotationDisplay, CombatAutorotationFilter>(Order = 6, Section = "combat")]
    public CombatAutorotation CombatAutorotation { get; set; } = CombatAutorotation.WrathCombo;


    /// <summary>
    ///     Stay mounted while a CE is preparing; dismount when it starts.
    /// </summary>
    [Checkbox(Order = 9, Section = "travel")]
    public bool StayMountedWhileWaitingForCe { get; set; } = false;

    /// <summary>
    ///     After FATE/CE: Return, teleport to the nearest aetheryte for the next activity, mount,
    ///     then stop — no auto-walk.
    /// </summary>
    [Checkbox(Order = 10, Section = "travel")]
    public bool StopAfterReturn { get; set; } = false;

    /// <summary>
    ///     When the current phantom job is maxed, switch to the next unlocked non-maxed job.
    /// </summary>
    [Checkbox(Order = 11, Section = "jobs")]
    public bool PhantomJobsLevelingMode { get; set; } = false;

    /// <summary>
    ///     After FATE/CE: if raisable corpses are nearby, raise with the selected phantom job then continue.
    ///     No bodies → no swap / no wait; Illegal Mode continues as usual.
    /// </summary>
    [Checkbox(Order = 12, Section = "triage")]
    public bool EnableTriageMode { get; set; } = false;

    /// <summary>Which phantom job Triage Mode swaps to for raises (falls back if not unlocked).</summary>
    [TriageRaiseJob(Order = 13, Section = "triage")]
    public TriageRaiseJobPreference PreferredTriageRaiseJob { get; set; } = TriageRaiseJobPreference.PhantomChemist;

    /// <summary>
    ///     Session hint for Completionist UI (run mode is the source of truth while active).
    /// </summary>
    public bool EnableCompletionistMode { get; set; } = false;

    /// <summary>
    ///     Illegal Mode / Completionist: after CE/FATE, Sight (if known) then hunt, or map hunt
    ///     without Sight. Only Illegal Mode reads this, so it belongs here rather than on the
    ///     Treasure page where people configuring Illegal Mode would not find it.
    /// </summary>
    [Checkbox(Order = 13, Section = "treasure")]
    public bool EnableAutomaticTreasureHuntDuringIllegalMode { get; set; } = false;

    [Checkbox(Order = 14, Section = "treasure")]
    public bool ShouldCastTreasureSight { get; set; } = false;

    [IntRange(60, 600, Order = 15, Section = "treasure")]
    public int TreasureSightRecastIntervalSeconds { get; set; } = 120;

    /// <summary>Max random idle before Return; 0 delay when Treasure Sight is latched.</summary>
    [IntRange(2, 60, Order = 16, Section = "delays")]
    public int MaxRemoteIdleTimeSeconds { get; set; } = 10;

    /// <summary>
    ///     Upper bound (seconds) for a random 0..max idle at camp before teleporting to a FATE/CE.
    ///     0 = leave immediately.
    /// </summary>
    [IntRange(0, 60, Order = 17, Section = "delays")]
    public int MaxBaseTeleportDelaySeconds { get; set; } = 0;

    /// <summary>
    ///     Repair equipped gear when any piece falls to or below this condition (%).
    /// </summary>
    [IntRange(1, 99, Order = 18, Section = "repair")]
    public int AutoRepairThreshold { get; set; } = 30;
}
