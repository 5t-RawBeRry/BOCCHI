using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using Ocelot.Extensions;
using System.Numerics;

namespace BOCCHI.Automator.Services;

public static class AutoMount
{
    public static void MaybeRemount(
        AutomatorConfig config,
        ICondition conditions,
        IObjectTable objects,
        Vector3 destination)
    {
        if (!config.ShouldAutoMount)
        {
            return;
        }

        if (conditions[ConditionFlag.Mounted]
            || conditions[ConditionFlag.Mounting]
            || conditions[ConditionFlag.InCombat]
            || conditions[ConditionFlag.Unconscious])
        {
            return;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return;
        }

        if (player.Position.Distance(destination) <= NavigationConstants.MountMinDistance)
        {
            return;
        }

        if (!EzThrottler.Throttle("Automator::AutoMount", 750))
        {
            return;
        }

        MountWait.TryCast((uint)Math.Max(0, config.PreferredMountId));
    }
}
