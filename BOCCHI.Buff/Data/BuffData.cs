using BOCCHI.Common.Config;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.SupportJobs;
using Ocelot.Actions;
using Action = Ocelot.Actions.Action;

namespace BOCCHI.Buff.Data;

public readonly struct BuffData
{
    public uint StatusId { get; init; }

    public SupportJobId SupportJobId { get; init; }

    public uint RequiredLevel { get; init; }

    public Func<BuffConfig, bool> ShouldApply { get; init; }

    public BuffState State { get; init; }

    public Action Action { get; init; }

    public static readonly BuffData RomeosBallad = new()
    {
        StatusId = PhantomBuffs.RomeosBallad,
        RequiredLevel = 2,
        SupportJobId = SupportJobId.PhantomBard,
        ShouldApply = config => config.ShouldApplyRomeosBallad(),
        State = BuffState.ApplyingRomeosBallad,
        Action = Actions.PhantomActionII // Romeo's Ballad
    };

    public static readonly BuffData Fleetfooted = new()
    {
        StatusId = PhantomBuffs.Fleetfooted,
        RequiredLevel = 3,
        SupportJobId = SupportJobId.PhantomMonk,
        ShouldApply = config => config.ShouldApplyFleetfooted(),
        State = BuffState.ApplyingFleetfooted,
        Action = Actions.PhantomActionIII // Counterstance
    };

    public static readonly BuffData EnduringFortitude = new()
    {
        StatusId = PhantomBuffs.EnduringFortitude,
        RequiredLevel = 2,
        SupportJobId = SupportJobId.PhantomKnight,
        ShouldApply = config => config.ShouldApplyEnduringFortitude(),
        State = BuffState.ApplyingEnduringFortitude,
        Action = Actions.PhantomActionII // Pray
    };

    public static readonly BuffData QuickerStep = new()
    {
        StatusId = PhantomBuffs.QuickerStep,
        RequiredLevel = 2,
        SupportJobId = SupportJobId.PhantomDancer,
        ShouldApply = config => config.ShouldApplyQuickerStep(),
        State = BuffState.ApplyingQuickerStep,
        Action = Actions.PhantomActionII // Quickstep
    };

    public static readonly IEnumerable<BuffData> All =
    [
        RomeosBallad,
        Fleetfooted,
        EnduringFortitude,
        QuickerStep
    ];
}
