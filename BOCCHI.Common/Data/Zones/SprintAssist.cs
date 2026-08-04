using Ocelot.Actions;

namespace BOCCHI.Common.Data.Zones;

/// <summary>Cast general Sprint when available (e.g. on-foot walks to base aetheryte).</summary>
public static class SprintAssist
{
    public static void MaybeCast(bool enabled = true)
    {
        if (!enabled || !Actions.Sprint.CanCast())
        {
            return;
        }

        Actions.Sprint.Cast();
    }
}
