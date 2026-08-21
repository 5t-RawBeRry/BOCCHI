using Newtonsoft.Json.Linq;

namespace BOCCHI.Common.Config.Migrations;

/// <summary>
///     Move bronze/silver hunt fill thresholds from Mob Farmer yields onto TreasureConfig so Illegal
///     Mode auto-hunt and Mob Farmer share the same sliders.
/// </summary>
public class ConfigMigratorV23ToV24 : IMigrator
{
    public int FromVersion => 23;

    public int ToVersion => 24;

    public JObject Migrate(JObject oldConfig)
    {
        JObject result = (JObject)oldConfig.DeepClone();
        result["Version"] = ToVersion;

        JObject treasure = JObjectExtensions.EnsureObject(
            result, "TreasureConfig", "BOCCHI.Common.Config.TreasureConfig, BOCCHI.Common");

        if (result["MobFarmerConfig"] is not JObject farmer)
        {
            return result;
        }

        if (farmer["TreasureHuntMinBronzePercent"] is JToken bronze)
        {
            treasure["HuntMinBronzePercent"] = bronze.DeepClone();
            farmer.Remove("TreasureHuntMinBronzePercent");
        }

        if (farmer["TreasureHuntMinSilverPercent"] is JToken silver)
        {
            treasure["HuntMinSilverPercent"] = silver.DeepClone();
            farmer.Remove("TreasureHuntMinSilverPercent");
        }

        return result;
    }
}
