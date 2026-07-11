using BOCCHI.Buff.Data;
using BOCCHI.Buff.Services;
using BOCCHI.Common.Data.SupportJobs;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;

namespace BOCCHI.Buff.StateMachine.Handlers.ApplyingBuff;

public class ApplyingEnduringFortitudeHandler(
    IBuffProvider buffs,
    IObjectTable objects,
    ICondition conditions,
    ISupportJobFactory supportJobs,
    ISupportJobChanger changer
) : BaseHandler(BuffState.ApplyingEnduringFortitude, buffs, objects, conditions, supportJobs, changer);
