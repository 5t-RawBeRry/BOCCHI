using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Mobs;
using BOCCHI.Common.Extensions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;

namespace BOCCHI.MobFarmer.Services;

public class MobScanner(
    MobFarmerConfig config,
    IObjectTable objects,
    IPlayer player
) : IMobScanner
{
    public IReadOnlyList<IBattleNpc> Mobs { get; private set; } = [];

    public IEnumerable<IBattleNpc> InCombat
    {
        get
        {
            var localPlayer = objects.LocalPlayer;
            return Mobs.Where(o => o.IsTargetingPlayer(localPlayer));
        }
    }

    public IEnumerable<IBattleNpc> NotInCombat
    {
        get => Mobs.Where(o => !o.HasTarget());
    }

    public unsafe void Update()
    {
        if (objects.LocalPlayer is not { } localPlayer)
        {
            Mobs = [];
            return;
        }

        Mobs = objects.OfType<IBattleNpc>()
            .Where(o => o is { IsDead: false, IsTargetable: true })
            .Where(o => o.IsHostile())
            .Where(o => player.Position.Distance2D(o.Position) <= config.MaxEuclideanDistance)
            .Where(o =>
            {
                var battleChara = (BattleChara*)o.Address;
                if (battleChara->ForayInfo.Level > config.MaxMobLevel)
                {
                    return false;
                }

                if (MobData.IsSelected(o.NameId, config.Mobs))
                {
                    return true;
                }

                if (!config.ConsiderSpecialMobs)
                {
                    return false;
                }

                return MobData.TryFromNameId(o.NameId, out var mob) && MobData.MobsWithSpawnCondition.Contains(mob);
            })
            .ToList();
    }
}
