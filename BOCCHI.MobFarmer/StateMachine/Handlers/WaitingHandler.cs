using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Mobs;
using BOCCHI.MobFarmer.Data;
using BOCCHI.MobFarmer.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
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
        // Always defend if something is already on us.
        if (scanner.InCombat.Any())
        {
            return FarmerPhase.Fighting;
        }

        if (config.OnlyStartOutOfCombat && conditions[ConditionFlag.InCombat])
        {
            return null;
        }

        if (conditions[ConditionFlag.InCombat])
        {
            return FarmerPhase.Fighting;
        }

        // Free (untargeted) selected mobs only — contested packs must not start a loop.
        int free = CountTowardMinimum(scanner.NotInCombat);
        if (free == 0)
        {
            return null;
        }

        return free >= config.MinimumMobsToStartLoop ? FarmerPhase.Buffing : null;
    }

    private int CountTowardMinimum(IEnumerable<IBattleNpc> mobs)
    {
        if (config.CountSpecialMobsTowardMinimum)
        {
            return mobs.Count();
        }

        return mobs.Count(m => !MobData.IsSpecialMob(m.NameId));
    }
}
