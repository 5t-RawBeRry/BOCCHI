using Dalamud.Game.ClientState.Objects.Types;

namespace BOCCHI.MobFarmer.Services;

public interface IMobScanner
{
    IReadOnlyList<IBattleNpc> Mobs { get; }

    IEnumerable<IBattleNpc> InCombat { get; }

    IEnumerable<IBattleNpc> NotInCombat { get; }

    void Update();
}
