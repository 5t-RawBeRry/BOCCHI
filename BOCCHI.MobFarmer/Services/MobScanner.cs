using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Data.Mobs;
using BOCCHI.Common.Extensions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Ocelot.Extensions;
using Ocelot.Services.PlayerState;

namespace BOCCHI.MobFarmer.Services;

public class MobScanner
(
    MobFarmerConfig config,
    IObjectTable objects,
    IPlayer player,
    IZoneProvider zones
) : IMobScanner
{
    public IReadOnlyList<IBattleNpc> Mobs { get; private set; } = [];

    public IEnumerable<IBattleNpc> InCombat
    {
        get
        {
            IPlayerCharacter? localPlayer = objects.LocalPlayer;
            return Mobs.Where(o => o.IsTargetingPlayer(localPlayer));
        }
    }

    public IEnumerable<IBattleNpc> NotInCombat => Mobs.Where(o => !o.HasTarget());

    public unsafe void Update()
    {
        // Occult Crescent only (the farmer panel still previews counts while stopped).
        if (!zones.GetZone().IsOccultCrescentZone())
        {
            if (Mobs.Count > 0)
            {
                Mobs = [];
            }

            return;
        }

        if (objects.LocalPlayer is not { } localPlayer)
        {
            Mobs = [];
            return;
        }

        Mobs = objects.OfType<IBattleNpc>()
            .Where(o => o is { IsDead: false, IsTargetable: true })
            .Where(o => player.Position.Distance2D(o.Position) <= config.MaxEuclideanDistance)
            .Where(o =>
            {
                BattleChara* battleChara = (BattleChara*)o.Address;
                // Level 0 = foray info unavailable; don't filter those out.
                byte level = battleChara->ForayInfo.Level;
                if (level > 0 && level > config.MaxMobLevel)
                {
                    return false;
                }

                // Selected OC NameIds count even when not flagged hostile yet (common in caves).
                if (MobData.IsSelected(o.NameId, config.Mobs))
                {
                    return true;
                }

                if (!o.IsHostile())
                {
                    return false;
                }

                if (!config.ConsiderSpecialMobs)
                {
                    return false;
                }

                return MobData.TryFromNameId(o.NameId, out Mob mob) && MobData.MobsWithSpawnCondition.Contains(mob);
            })
            .ToList();
    }
}
