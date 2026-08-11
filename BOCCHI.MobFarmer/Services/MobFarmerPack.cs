using BOCCHI.Common.Data.Mobs;
using Dalamud.Game.ClientState.Objects.Types;

namespace BOCCHI.MobFarmer.Services;

internal static class MobFarmerPack
{
    public static int CountTowardMinimum(IEnumerable<IBattleNpc> mobs, bool countSpecials) =>
        countSpecials
            ? mobs.Count()
            : mobs.Count(m => !MobData.IsSpecialMob(m.NameId));
}
