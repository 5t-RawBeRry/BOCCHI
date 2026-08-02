using BOCCHI.Common.Data.Fates;

namespace BOCCHI.Common.Services;

public interface IFateScorer
{
    FateScore Score(Fate fate);

    Fate? SelectBest(IReadOnlyList<Fate> fates);
}
