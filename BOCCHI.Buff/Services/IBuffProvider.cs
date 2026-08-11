using BOCCHI.Buff.Data;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace BOCCHI.Buff.Services;

public interface IBuffProvider
{
    IEnumerable<BuffData> GetBuffs();

    BuffData GetBuffForState(BuffState state);

    bool ShouldRefreshAny();

    bool CanUseInquiringMind();

    /// <summary>Selected buffs Inquiring Mind can grant that still need a refresh.</summary>
    IEnumerable<BuffData> GetInquiringMindTargetsNeedingRefresh(IPlayerCharacter player, uint maxFreshMinutes);

    bool NeedsInquiringMind(IPlayerCharacter player, uint maxFreshMinutes);

    /// <summary>True when every Inquiring Mind target for this run is freshly applied.</summary>
    bool AreInquiringMindTargetsFresh(IPlayerCharacter player);
}
