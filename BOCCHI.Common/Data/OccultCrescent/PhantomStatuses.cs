namespace BOCCHI.Common.Data.OccultCrescent;

/// <summary>
///     Phantom combat buff IDs used by BOCCHI. Job-identity statuses stay on
///     <see cref="SupportJobs.SupportJob.StatusId"/>.
/// </summary>
public static class PhantomBuffs
{
    public const ushort EnduringFortitude = 4233;

    public const ushort Fleetfooted = 4239;

    public const ushort RomeosBallad = 4244;

    public const ushort BattleBell = 4251;

    public const ushort BattlesClangor = 4252;

    /// <summary>Phantom Geomancer Ringing Respite (60s HoT-on-hit).</summary>
    public const ushort RingingRespite = 4253;

    /// <summary>Knowledge-crystal party buff from Dancer Quickstep / Freelancer Inquiring Mind (30m).</summary>
    public const ushort QuickerStep = 4799;
}

public static class PhantomDebuffs
{
    public const ushort FireWeakness = 5322;
    public const ushort IceWeakness = 5323;
    public const ushort LightningWeakness = 5324;
    public const ushort WindWeakness = 5325;
}

/// <summary>Common player statuses used by Occult automation (not phantom-job exclusives).</summary>
public static class PlayerStatuses
{
    /// <summary>Pending raise prompt on a corpse — skip these for Triage Mode.</summary>
    public const ushort Raise = 148;
}
