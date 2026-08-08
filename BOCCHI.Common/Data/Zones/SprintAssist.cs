using Ocelot.Actions;

namespace BOCCHI.Common.Data.Zones;

/// <summary>Cast general Sprint when available (e.g. on-foot walks to an aetheryte).</summary>
public static class SprintAssist
{
    /// <param name="inBasecamp">Never sprint in base camp — short walks don't need it.</param>
    public static void MaybeCast(bool enabled = true, bool inBasecamp = false)
    {
        if (!enabled || inBasecamp || !Actions.Sprint.CanCast())
        {
            return;
        }

        Actions.Sprint.Cast();
    }
}
