using Newtonsoft.Json.Linq;

namespace BOCCHI.Common.Config.Migrations;

/// <summary>
///     Drop LastSouthHornStartHalf. South Horn rotates through authored segments instead of
///     alternating red/blue, and the old "red"/"blue" value has no segment equivalent — the next
///     hunt simply starts at the first segment and rotates from there.
/// </summary>
public class ConfigMigratorV19ToV20 : IMigrator
{
    public int FromVersion => 19;

    public int ToVersion => 20;

    public JObject Migrate(JObject oldConfig)
    {
        JObject result = (JObject)oldConfig.DeepClone();
        result["Version"] = ToVersion;

        if (result["TreasureConfig"] is JObject treasure)
        {
            treasure.Remove("LastSouthHornStartHalf");
        }

        return result;
    }
}
