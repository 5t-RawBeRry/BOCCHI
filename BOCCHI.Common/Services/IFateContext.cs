using BOCCHI.Common.Data.Fates;
using Dalamud.Game.ClientState.Objects.Types;

namespace BOCCHI.Common.Services;

public interface IFateContext
{
    bool IsInFate();

    FateId? GetFateId();

    /// <summary>
    ///     True if you are targeting a hostile of this FATE, or one of them is targeting you.
    ///     Works before CurrentFate is set (rim pull).
    /// </summary>
    bool IsInCombatWith(FateId id);

    IEnumerable<IBattleNpc> GetTargets();
}
