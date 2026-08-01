using Dalamud.Configuration;
namespace BOCCHI.Common.Config;

public interface IConfiguration : IPluginConfiguration
{
    TrackerConfig TrackerConfig { get; set; }

    UIConfig UIConfig { get; set; }

    AutomatorConfig AutomatorConfig { get; set; }

    BuffConfig BuffConfig { get; set; }

    CombatConfig CombatConfig { get; set; }

    MobFarmerConfig MobFarmerConfig { get; set; }

    FatesConfig FatesConfig { get; set; }

    CriticalEncountersConfig CriticalEncountersConfig { get; set; }

    TreasureConfig TreasureConfig { get; set; }
}
