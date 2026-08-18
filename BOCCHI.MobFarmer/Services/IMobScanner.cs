using Dalamud.Game.ClientState.Objects.Types;

namespace BOCCHI.MobFarmer.Services;

public interface IMobScanner
{
    IReadOnlyList<IBattleNpc> Mobs { get; }

    IEnumerable<IBattleNpc> InCombat { get; }

    IEnumerable<IBattleNpc> NotInCombat { get; }

    /// <summary>Selected enemies that have a target that is not the local player.</summary>
    IEnumerable<IBattleNpc> Contested { get; }

    void Update();
}
