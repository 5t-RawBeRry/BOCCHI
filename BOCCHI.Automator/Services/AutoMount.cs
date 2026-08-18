using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace BOCCHI.Automator.Services;

public static class AutoMount
{
    public static void MaybeRemount(
        MovementConfig config,
        ICondition conditions,
        IObjectTable objects,
        Vector3 destination,
        bool inBaseCamp = false)
    {
        MountWait.TryCastIfNeeded(
            conditions,
            objects,
            destination,
            config.ShouldAutoMount,
            config.PreferredMountId,
            inBaseCamp);
    }
}
