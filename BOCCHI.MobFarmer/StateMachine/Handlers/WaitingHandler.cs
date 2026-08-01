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
        if (conditions[ConditionFlag.InCombat] || scanner.InCombat.Any())
        {
            return FarmerPhase.Fighting;
        }

        // Free (untargeted) selected mobs only — contested packs must not start a loop.
        int free = scanner.NotInCombat.Count();
        if (free == 0)
        {
            return null;
        }

        return free >= config.MinimumMobsToStartLoop ? FarmerPhase.Buffing : null;
    }
}
