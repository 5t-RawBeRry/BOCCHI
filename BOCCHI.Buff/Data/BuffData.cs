using BOCCHI.Common.Config;
using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Data.SupportJobs;
using Ocelot.Actions;
using Action = Ocelot.Actions.Action;

namespace BOCCHI.Buff.Data;

public readonly struct BuffData
{
    public uint StatusId => State switch
    {
        BuffState.ApplyingRomeosBallad => PhantomBuffs.RomeosBallad,
        BuffState.ApplyingFleetfooted => PhantomBuffs.Fleetfooted,
        BuffState.ApplyingEnduringFortitude => PhantomBuffs.EnduringFortitude,
        BuffState.ApplyingQuickerStep => PhantomBuffs.QuickerStep,
        _ => 0
    };

    public SupportJobId SupportJobId { get; init; }

    public uint RequiredLevel => State switch
    {
        BuffState.ApplyingRomeosBallad => PhantomActions.RomeosBalladUnlock,
        BuffState.ApplyingFleetfooted => PhantomActions.CounterstanceUnlock,
        BuffState.ApplyingEnduringFortitude => PhantomActions.PrayUnlock,
        BuffState.ApplyingQuickerStep => PhantomActions.QuickstepUnlock,
        _ => 1
    };

    public Func<BuffConfig, bool> ShouldApply { get; init; }

    public BuffState State { get; init; }

    public Action Action { get; init; }

    public static readonly BuffData RomeosBallad = new()
    {
        SupportJobId = SupportJobId.PhantomBard,
        ShouldApply = config => config.ShouldApplyRomeosBallad(),
        State = BuffState.ApplyingRomeosBallad,
        Action = Actions.PhantomActionII // Romeo's Ballad
    };

    public static readonly BuffData Fleetfooted = new()
    {
        SupportJobId = SupportJobId.PhantomMonk,
        ShouldApply = config => config.ShouldApplyFleetfooted(),
        State = BuffState.ApplyingFleetfooted,
        Action = Actions.PhantomActionIII // Counterstance
    };

    public static readonly BuffData EnduringFortitude = new()
    {
        SupportJobId = SupportJobId.PhantomKnight,
        ShouldApply = config => config.ShouldApplyEnduringFortitude(),
        State = BuffState.ApplyingEnduringFortitude,
        Action = Actions.PhantomActionII // Pray
    };

    public static readonly BuffData QuickerStep = new()
    {
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
