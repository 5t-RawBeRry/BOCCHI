using BOCCHI.Common.Config;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Ocelot.States.Flow;

namespace BOCCHI.MobFarmer.StateMachine.Handlers;

public class WaitingHandler
(
    MobFarmerConfig config,
    IMobScanner scanner,
    ICondition conditions
) : FlowStateHandler<FarmerPhase>(FarmerPhase.Waiting)
{
    public override FarmerPhase? Handle()
    {
        if (conditions[ConditionFlag.InCombat])
        {
            return FarmerPhase.Fighting;
        }

        return scanner.Mobs.Count >= config.MinimumMobsToStartLoop ? FarmerPhase.Buffing : null;
    }
}
