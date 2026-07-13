using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;

namespace BOCCHI.Buff.StateMachine.Handlers.ApplyingBuff;

public class ApplyingRomeosBalladHandler(
    IBuffProvider buffs,
    IObjectTable objects,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer
) : BaseHandler(BuffState.ApplyingRomeosBallad, buffs, objects, conditions, supportJobs, changer);

public class ApplyingEnduringFortitudeHandler(
    IBuffProvider buffs,
    IObjectTable objects,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer
) : BaseHandler(BuffState.ApplyingEnduringFortitude, buffs, objects, conditions, supportJobs, changer);

public class ApplyingFleetfootedHandler(
    IBuffProvider buffs,
    IObjectTable objects,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer
) : BaseHandler(BuffState.ApplyingFleetfooted, buffs, objects, conditions, supportJobs, changer);

public class ApplyingQuickerStepHandler(
    IBuffProvider buffs,
    IObjectTable objects,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer
) : BaseHandler(BuffState.ApplyingQuickerStep, buffs, objects, conditions, supportJobs, changer);
