using Newtonsoft.Json.Linq;

namespace BOCCHI.Common.Config.Migrations;

/// <summary>
///     Move the travel settings every module reads out of AutomatorConfig into their own Movement
///     group. They are not Illegal Mode options — Treasure Hunt, Carrot Hunt and Mob Farmer all
///     honour them — and living under Automator made them undiscoverable.
///     Illegal-Mode-only travel settings stay where they are.
/// </summary>
public class ConfigMigratorV22ToV23 : IMigrator
{
    public int FromVersion => 22;

    public int ToVersion => 23;

    public JObject Migrate(JObject oldConfig)
    {
        JObject result = (JObject)oldConfig.DeepClone();
        result["Version"] = ToVersion;

        JObject movement = JObjectExtensions.EnsureObject(
            result, "MovementConfig", "BOCCHI.Common.Config.MovementConfig, BOCCHI.Common");

        if (result["AutomatorConfig"] is not JObject automator)
        {
            return result;
        }

        string[] moved =
        [
            "SprintOnAetheryteApproach",
            "ShouldAutoMount",
            "PreferredMountId",
            "ShouldJumpWhenStuck",
            "JumpWhenStuckSeconds",
        ];

        JObjectExtensions.MoveIfPresent(automator, movement, moved);
        foreach (string key in moved)
        {
            automator.Remove(key);
        }

        // Auto treasure hunt is read only by Illegal Mode, so it moves the other way: off the
        // Treasure page and onto Automator, beside the Treasure Sight options it works with.
        if (result["TreasureConfig"] is JObject treasure)
        {
            const string autoHunt = "EnableAutomaticTreasureHuntDuringIllegalMode";
            JObjectExtensions.MoveIfPresent(treasure, automator, autoHunt);
            treasure.Remove(autoHunt);
        }

        return result;
    }
}
