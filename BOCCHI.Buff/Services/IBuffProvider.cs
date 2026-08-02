using BOCCHI.Buff.Data;
using BOCCHI.Common.Data.OccultCrescent;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace BOCCHI.Buff.Services;

public interface IBuffProvider
{
    IEnumerable<BuffData> GetBuffs();

    BuffData GetBuffForState(BuffState state);

    bool ShouldRefreshAny();

    bool CanUseInquiringMind();

    bool NeedsInquiringMind(IPlayerCharacter player, uint maxFreshMinutes);

    bool IsInquiringMindFresh(IPlayerCharacter player);
}
