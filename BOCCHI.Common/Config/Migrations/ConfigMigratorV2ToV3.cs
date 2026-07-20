using Newtonsoft.Json.Linq;
namespace BOCCHI.Common.Config.Migrations;

public class ConfigMigratorV2ToV3 : IMigrator
{
    public int FromVersion { get; } = 2;

    public int ToVersion { get; } = 3;

    public JObject Migrate(JObject oldConfig)
    {
        JObject result = (JObject)oldConfig.DeepClone();
        result["Version"] = ToVersion;

        int trackedDuration = oldConfig.IntOr("ExperienceConfig.TrackedDuration", oldConfig.IntOr("CurrencyConfig.TrackedDuration", 5));
        int graphBucketSize = oldConfig.IntOr("ExperienceConfig.GraphBucketSize", oldConfig.IntOr("CurrencyConfig.GraphBucketSize", 15));

        result.Remove("ExperienceConfig");
        result.Remove("CurrencyConfig");

        result["TrackerConfig"] = new JObject
        {
            ["$type"] = "BOCCHI.Common.Config.TrackerConfig, BOCCHI.Common",
            ["TrackedDuration"] = trackedDuration,
            ["GraphBucketSize"] = graphBucketSize
        };

        return result;
    }
}
