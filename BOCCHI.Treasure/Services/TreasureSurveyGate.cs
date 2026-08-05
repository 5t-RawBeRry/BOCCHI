namespace BOCCHI.Treasure.Services;

/// <summary>
///     Survey count gating for automatic treasure hunts (AOCCH-style thresholds).
/// </summary>
public static class TreasureSurveyGate
{
    /// <summary>
    ///     Threshold 0 means "any (&gt; 0)" for that tier.
    ///     Both thresholds 0 means any silver+bronze &gt; 0.
    /// </summary>
    public static bool MeetsThresholds(int silver, int bronze, int silverThreshold, int bronzeThreshold)
    {
        if (silverThreshold <= 0 && bronzeThreshold <= 0)
        {
            return silver + bronze > 0;
        }

        return MeetsThreshold(silverThreshold, silver) && MeetsThreshold(bronzeThreshold, bronze);
    }

    public static bool MeetsThreshold(int configuredThreshold, int observedCount) =>
        configuredThreshold <= 0 ? observedCount > 0 : observedCount >= configuredThreshold;

    /// <summary>How many more of this tier are needed before thresholds can pass.</summary>
    public static int Deficit(int configuredThreshold, int observedCount)
    {
        int required = configuredThreshold <= 0 ? 1 : configuredThreshold;
        return Math.Max(0, required - observedCount);
    }
}
