using Newtonsoft.Json.Linq;

namespace BOCCHI.Common.Config.Migrations;

/// <summary>Move skip-by-progress from pot timing onto FATEs (applies to all FATEs).</summary>
public class ConfigMigratorV21ToV22 : IMigrator
{
    public int FromVersion => 21;

    public int ToVersion => 22;

    public JObject Migrate(JObject oldConfig)
    {
        JObject result = (JObject)oldConfig.DeepClone();
        result["Version"] = ToVersion;

        JObject fates = JObjectExtensions.EnsureObject(
            result, "FatesConfig", "BOCCHI.Common.Config.FatesConfig, BOCCHI.Common");

        if (result["PotsConfig"] is JObject pots && pots["MaxPotFateProgressPercent"] is JToken value)
        {
            fates["MaxFateProgressPercent"] = value.DeepClone();
            pots.Remove("MaxPotFateProgressPercent");
        }

        return result;
    }
}
