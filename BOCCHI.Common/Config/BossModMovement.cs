using System.Globalization;
using Ocelot.Rotation.Services;

namespace BOCCHI.Common.Config;

public enum BossModOverdodge
{
    None,
    Small,
    Medium,
    Large,
}

public enum BossModMovementDelay
{
    None,
    Short,
    Long,
}

public static class BossModMovement
{
    public const float MinRange = 1.1f;

    public const float MaxRange = 30f;

    public static BossModMovementSettings From(AutomatorConfig config, bool isMelee)
    {
        string range;
        if (!config.BossModMaxDistanceByRole)
        {
            range = FormatRange(config.BossModMaxDistance);
        }
        else if (isMelee && config.BossModMeleeOnHitbox)
        {
            range = "OnHitbox";
        }
        else if (isMelee)
        {
            range = FormatRange(config.BossModMaxDistanceMelee);
        }
        else
        {
            range = FormatRange(config.BossModMaxDistanceRanged);
        }

        return new(range, config.BossModOverdodge.ToString(), config.BossModMovementDelay.ToString());
    }

    public static string FormatRange(float yards)
    {
        float clamped = Math.Clamp(MathF.Round(yards, 1), MinRange, MaxRange);
        return clamped.ToString(CultureInfo.InvariantCulture);
    }
}
