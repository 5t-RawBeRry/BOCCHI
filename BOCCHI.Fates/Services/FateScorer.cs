using BOCCHI.Common.Config;
using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Dalamud.Plugin.Services;
using Ocelot.Extensions;

namespace BOCCHI.Fates.Services;

public class FateScorer(
    FatesConfig config,
    IZoneProvider zones,
    IObjectTable objects
) : IFateScorer
{
    private const float PotFateBonus = 250f;

    public FateScore Score(Fate fate)
    {
        var score = new FateScore();

        if (!config.IsFateEnabled(fate.Id.Value))
        {
            return score;
        }

        if (objects.LocalPlayer is not { } player)
        {
            return score;
        }

        var distance = player.Position.Distance2D(fate.Position);
        score.Add("distance", 1000f / (distance + 1f));
        score.Add("progress", Math.Max(0, 100 - fate.Progress));

        if (config.PreferPotFates && zones.GetZone().IsPotFate(fate.Id.Value))
        {
            score.Add("pot", PotFateBonus);
        }

        return score;
    }

    public Fate? SelectBest(IReadOnlyList<Fate> fates)
    {
        if (!config.ShouldDoFates || fates.Count == 0)
        {
            return null;
        }

        Fate? best = null;
        var bestScore = float.MinValue;

        foreach (var fate in fates)
        {
            if (!config.IsFateEnabled(fate.Id.Value))
            {
                continue;
            }

            var score = Score(fate);
            if (score.Value > bestScore)
            {
                bestScore = score.Value;
                best = fate;
            }
        }

        return best;
    }
}
