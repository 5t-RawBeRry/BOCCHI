using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Services;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using Ocelot.Extensions;

namespace BOCCHI.Fates.Services;

public class FateContext(IObjectTable objects) : IFateContext
{
    public bool IsInFate()
    {
        return GetFateId() != null;
    }

    public unsafe FateId? GetFateId()
    {
        var fateManager = FateManager.Instance();

        return fateManager != null && fateManager->CurrentFate != null ? new FateId(fateManager->CurrentFate->FateId) : null;
    }

    public IEnumerable<IBattleNpc> GetTargets()
    {
        var id = GetFateId();
        if (id == null)
        {
            return [];
        }

        var player = objects.LocalPlayer;
        if (player == null)
        {
            return [];
        }

        var fateId = id.Value.Value;

        return objects.OfType<IBattleNpc>()
            .Where(obj => obj is { IsDead: false, IsTargetable: true })
            .Where(o => o.IsHostile())
            .Where(obj =>
            {
                unsafe
                {
                    var battleChara = (BattleChara*)obj.Address;

                    return battleChara->FateId == fateId;
                }
            })
            .OrderBy(o => o.Position.Distance2D(player.Position));

    }
}
