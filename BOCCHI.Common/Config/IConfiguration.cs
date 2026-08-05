using Dalamud.Configuration;
namespace BOCCHI.Common.Config;

public interface IConfiguration : IPluginConfiguration
{
    TrackerConfig TrackerConfig { get; set; }

    UIConfig UIConfig { get; set; }

    DependenciesConfig DependenciesConfig { get; set; }

    AutomatorConfig AutomatorConfig { get; set; }

    BuffConfig BuffConfig { get; set; }

    MobFarmerConfig MobFarmerConfig { get; set; }

    FatesConfig FatesConfig { get; set; }

    PotsConfig PotsConfig { get; set; }

    CriticalEncountersConfig CriticalEncountersConfig { get; set; }

    TreasureConfig TreasureConfig { get; set; }

    EventDropConfig EventDropConfig { get; set; }

    ForkedTowerConfig ForkedTowerConfig { get; set; }

    ShoppingConfig ShoppingConfig { get; set; }
}
