using BOCCHI.Common.Config.Fields;
using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation", GroupOrder = 0, Order = 0)]
public class AutomatorConfig : IAutoConfig
{
    [Checkbox(Order = 0)]
    public bool ShouldDoFates { get; set; } = true;

    [Checkbox(Order = 1)]
    public bool PreferPotFates { get; set; } = false;

    [Checkbox(Order = 2)]
    public bool ShouldFarmPotChests { get; set; } = false;

    [Checkbox(Order = 3)]
    public bool ShouldPrepositionToPots { get; set; } = true;

    [Checkbox(Order = 4)]
    public bool ShouldDoCriticalEncounters { get; set; } = true;

    /// <summary>Use BOCCHI AI BossMod preset in FATE/CE combat (off while pathfinding).</summary>
    [Checkbox(Order = 5)]
    public bool ToggleAiProvider { get; set; } = true;

    [Checkbox(Order = 6)]
    public bool ShouldAutoMount { get; set; } = true;

    /// <summary>Preferred mount sheet row ID. 0 = Mount Roulette.</summary>
    [MountSelect(Order = 7)]
    public uint PreferredMountId { get; set; } = 0;

    /// <summary>Use Sprint on foot when closing in on an aetheryte (#129).</summary>
    [Checkbox(Order = 8)]
    public bool SprintOnAetheryteApproach { get; set; } = true;

    /// <summary>
    ///     Stay mounted while a CE is preparing; dismount when it starts (#127).
    /// </summary>
    [Checkbox(Order = 9)]
    public bool StayMountedWhileWaitingForCe { get; set; } = false;

    /// <summary>
    ///     After FATE/CE: Return, teleport to the nearest aetheryte for the next activity, mount,
    ///     then stop — no auto-walk (#139 / replaces #109 full-path requirement).
    /// </summary>
    [Checkbox(Order = 10)]
    public bool StopAfterReturn { get; set; } = false;

    /// <summary>
    ///     When the current phantom job is maxed, switch to the next unlocked non-maxed job.
    /// </summary>
    [Checkbox(Order = 11)]
    public bool PhantomJobsLevelingMode { get; set; } = false;

    /// <summary>
    ///     After a FATE/CE ends, switch to Phantom Chemist and raise nearby dead players
    ///     (skip anyone who already has a raise pending).
    /// </summary>
    [Checkbox(Order = 12)]
    public bool EnableTriageMode { get; set; } = false;

    [Checkbox(Order = 13)]
    public bool ShouldCastTreasureSight { get; set; } = false;

    [IntRange(60, 600, Order = 14)]
    public int TreasureSightRecastIntervalSeconds { get; set; } = 120;

    /// <summary>
    ///     Upper bound (seconds) for the random 2..max wait before Return after a FATE/CE.
    /// </summary>
    [IntRange(2, 60, Order = 15)]
    public int MaxRemoteIdleTimeSeconds { get; set; } = 10;

    /// <summary>
    ///     Upper bound (seconds) for a random 0..max idle at camp before teleporting to a FATE/CE (#138).
    ///     0 = leave immediately.
    /// </summary>
    [IntRange(0, 60, Order = 16)]
    public int MaxBaseTeleportDelaySeconds { get; set; } = 0;

    /// <summary>
    ///     Repair equipped gear when any piece falls to or below this condition (%).
    /// </summary>
    [IntRange(1, 99, Order = 17)]
    public int AutoRepairThreshold { get; set; } = 30;
}
