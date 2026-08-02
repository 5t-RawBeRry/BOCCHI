using BOCCHI.Common.Config;

namespace BOCCHI.Automator.Services;

/// <summary>Random pause before Return after a FATE/CE (humanize; OC has no Return CD).</summary>
public static class ReturnDelay
{
    /// <summary>
    ///     Uniform roll in [2, max] seconds inclusive, where max is
    ///     <see cref="AutomatorConfig.MaxRemoteIdleTimeSeconds"/> (clamped to at least 2).
    /// </summary>
    public static TimeSpan Roll(AutomatorConfig config)
    {
        int maxSeconds = Math.Max(2, config.MaxRemoteIdleTimeSeconds);
        return TimeSpan.FromSeconds(Random.Shared.Next(2, maxSeconds + 1));
    }
}
