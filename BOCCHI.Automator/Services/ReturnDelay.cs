using BOCCHI.Common.Config;
using Ocelot.Actions;

namespace BOCCHI.Automator.Services;

/// <summary>Random pause before Return after a FATE/CE (humanize).</summary>
public static class ReturnDelay
{
    /// <summary>
    ///     Overworld Return CD carries into Occult Crescent. Recast above this means Return is
    ///     genuinely unavailable — not just mounted / occupied (<see cref="Ocelot.Actions.Action.CanCast"/>).
    /// </summary>
    public const float CooldownRecastSeconds = 3f;

    public static bool IsOnCooldown() =>
        Actions.Return.GetRecastTime() > CooldownRecastSeconds;

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

/// <summary>Random idle at camp before teleporting to a FATE/CE.</summary>
public static class BaseTeleportDelay
{
    /// <summary>
    ///     Uniform roll in [0, max] seconds inclusive.
    ///     Returns <see cref="TimeSpan.Zero"/> when max is 0 (feature off).
    /// </summary>
    public static TimeSpan Roll(AutomatorConfig config)
    {
        int maxSeconds = Math.Max(0, config.MaxBaseTeleportDelaySeconds);
        if (maxSeconds == 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(Random.Shared.Next(0, maxSeconds + 1));
    }
}
